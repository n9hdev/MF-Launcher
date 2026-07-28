# Anti-Cheat Engine — Multi-Layer Reputation Architecture

## Philosophy

Every layer produces **structured evidence only**. No detector assigns bans, confidence scores, or verdicts. A future **Verdict Engine** will combine evidence from all layers (Game Integrity, PE Analysis, YARA, Behavioral Monitor, Memory Scanner, ClamAV, Team Cymru, Community Reputation, Certificate Reputation) into the final confidence score and action.

```
Client File ──► Local Layers (instant, offline) ──► Cloud Layers (async, cached)
                                                          │
                                              Evidence from all layers
                                                          │
                                                    Verdict Engine
                                                          │
                                               ┌───────────┴───────────┐
                                               ▼                       ▼
                                           Allow                   Ban/Flag
```

---

## Layer 1 — Internal Hash Database + Reputation API

**Purpose**: Central lookup for every file ever seen. The backbone all other layers feed into.

**How it works**:
- Client sends `SHA256` + metadata (size, signer, product name, version, file path)
- Server checks `FileReputation` table
- Returns verdict: `safe`, `cheat`, `unknown`, `suspicious`
- Client caches result locally (never asks twice for same hash)
- When verdict is `unknown`, server queues for deeper analysis

**Database schema**:
```
FileReputationEntry
  SHA256 (PK)
  MD5
  FileSize
  ProductName
  FileVersion
  Signer
  FirstSeen (UTC)
  LastSeen (UTC)
  TimesSeen
  TimesFlagged
  UniquePlayers
  Verdict (safe/cheat/unknown/suspicious)
  LastAnalysisTime
  AnalysisNotes
  ConfidenceScore
  IsLocalOverride
```

**API endpoints**:
- `POST /api/reputation/lookup` — check a hash
- `POST /api/reputation/report` — submit scan result from client
- `POST /api/reputation/verdict` — admin sets manual verdict

**Files created**:
- `AntiCheat.Core/Data/Entities/FileReputationEntity.cs`
- `AntiCheat.Shared/Models/ReputationDto.cs`
- `AntiCheat.Core/Interfaces/IReputationService.cs`
- `AntiCheat.Core/Services/ReputationService.cs` (in-memory cache, 1hr TTL, LRU eviction)
- `AntiCheat.Api/Controllers/ReputationController.cs`

**Status**: ✅ COMPLETE

---

## Layer 2 — Game Integrity Verification

**Purpose**: Detect modified game files. Highest-value check for MTA:SA.

**How it works**:
- On first clean launch, hash all known game files (`gta_sa.exe`, `MTA.exe`, core DLLs)
- Store known-good hashes in DB (`GameFileHashes` table)
- On each scan, re-hash and compare
- Any mismatch → structured evidence for Verdict Engine

**Files created**:
- `AntiCheat.Core/Data/Entities/GameFileHashEntity.cs`
- `AntiCheat.Detection/Detectors/GameIntegrityDetector.cs` (IDetector, async, auto-baselines on first run)

**Status**: ✅ COMPLETE

---

## Layer 3 — Deep PE Analyzer

**Purpose**: Structural analysis of executable files — no network needed. Pure evidence extraction.

**Extracts**: See `PeAnalysisResult` for full schema (94 fields across 20+ categories).

| Category | Fields |
|----------|--------|
| DOS Header | Magic, LfaNew |
| NT Header | Signature, Offset |
| File Header | Machine (x86/x64/ARM), NumberOfSections, TimeDateStamp, Characteristics |
| Optional Header | Magic (PE32/PE32+), LinkerVersion, EntryPoint, ImageBase, Subsystem, DLL Characteristics, Stack/Heap sizes |
| Sections | Name, VirtualSize, RawSize, RVA, Characteristics, Entropy, Executable/Writable flags |
| Imports | DLLs, APIs, Dangerous API flags (25+ APIs across 8 categories), Delayed imports |
| Exports | Function names, forwarded functions, ordinal info |
| Resources | Icons, Version Info (Company, Product, Copyright, etc.), Manifest |
| Digital Signature | Signed/Unsigned, Subject, Issuer, Thumbprint, NotBefore, NotAfter, Self-signed, Revoked (offline), Chain status |
| Rich Header | XOR-decrypted entries, Product IDs mapped to 224+ known tools, SHA256 hash |
| Debug | PDB path (RSDS/NB10), GUID, Age |
| CLR | Native / .NET / Mixed-mode, version, flags |
| TLS | TLS callbacks present, callback addresses |
| Overlay | Exists, size, offset, entropy |
| Hashes | SHA256, SHA1, MD5, ImpHash |
| Packer Detection | UPX, Themida, VMProtect, MPRESS, ASPack, PEtite, PECompact, Enigma — heuristic fallback |
| Entropy | File entropy, per-section, overlay — max/min/avg |

**Key design decisions**:
- `PeAnalysisResult` is a single immutable output consumed by later layers
- Never classifies files as malicious — only collects evidence
- Modular sub-analyzers run in `Parallel.Invoke` for performance
- Full RVA → file offset translation for all directory entries

**Files created**:
- `AntiCheat.Core/Models/PeAnalysisModels.cs` — 20+ DTOs
- `AntiCheat.Core/Interfaces/IPeAnalysisService.cs`
- `AntiCheat.Core/Services/PeAnalysisService.cs` — ~1600 lines

**Status**: ✅ COMPLETE

---

## Layer 4 — YARA Rules (Signature Engine)

**Purpose**: Pattern-based detection using PeAnalysisResult evidence + process/file matching.

**How it works**:
- `ISignatureEngine` interface with `MatchPe()`, `MatchProcessName()`, `MatchFilePath()` methods
- `SignatureEngineService` loads rules from external JSON files at startup (`Rules/*.json`)
- Rules are hot-loadable — drop a new JSON file in the Rules directory, restart to pick it up
- Rules match against: imports, sections, entropy, hashes, packer, PDB, signature, process names, file paths
- Each rule returns structured evidence (no verdict)
- `YaraDetector` v2.0.0 performs process name + file path + PE analysis matching on every scan cycle

**Rule categories implemented**:
- `injection_api_set` — count of dangerous import APIs (OpenProcess/WPM/CRT/VAEx)
- `dangerous_import` — single dangerous API import
- `lua_dll` — binary imports any Lua DLL (lua5.1, luajit, etc.)
- `process_name` — process name glob matching (30+ MTA cheat names, CheatEngine, injectors)
- `packed_unsigned` — packed + unsigned binary
- `high_entropy` — file entropy > threshold
- `high_entropy_overlay` — overlay entropy > threshold
- `suspicious_pdb` — PDB path contains cheat/hack/inject keywords
- `self_signed_game_file` — self-signed cert on game-named file
- `suspicious_section_name` — section matches cheat patterns
- `rwx_section` — section is both executable and writable
- `low_entropy_code` — code section entropy < 1.0
- `rsrc_executable` — .rsrc marked executable
- `tls_callbacks` — TLS callbacks present
- `unsigned_dll_game_dir` — unsigned DLL in game directory

**Rule files**:
- `Rules/mta-injectors.json` — 10 rules for injection API detection
- `Rules/cheat-processes.json` — 16 rules for known cheat/injector processes
- `Rules/lua-executors.json` — 1 rule for Lua DLL detection
- `Rules/pe-anomalies.json` — 10 rules for PE structural anomalies
- `Rules/pdb-rules.json` — 1 rule for suspicious PDB paths

**Files**:
- `AntiCheat.Core/Interfaces/ISignatureEngine.cs` + `SignatureMatch` model
- `AntiCheat.Core/Services/SignatureEngineService.cs` — data-driven engine
- `AntiCheat.Core/Services/SignatureRuleLoader.cs` — JSON file loader
- `AntiCheat.Core/Models/SignatureRuleModel.cs` — JSON-serializable rule model
- `AntiCheat.Core/Rules/*.json` — 5 rule files with 38 total rules
- `AntiCheat.Detection/Detectors/YaraScanner.cs` — v2.0.0

**Status**: ✅ COMPLETE

---

## Layer 5 — Behavioral Monitor

**Purpose**: Runtime detection of processes interacting with the game process.

**Subsystems**:
1. **Handle Enumeration** — `NtQuerySystemInformation(SystemHandleInformation)` enumerates all open handles system-wide, `DuplicateHandle` + `GetProcessId` identifies which processes have handles to the game PID, access mask parsing detects dangerous rights (PROCESS_TERMINATE, PROCESS_CREATE_THREAD, PROCESS_VM_WRITE, PROCESS_ALL_ACCESS, etc.)
2. **Unsigned Module Detection** — `EnumProcessModules` + `GetModuleFileNameEx` enumerates DLLs loaded into the game process, checks each for Authenticode signature; presence of unsigned DLLs in game process is structural evidence
3. **Thread Enumeration** — `CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD)` enumerates threads belonging to the game, detection for excessive thread counts or threads originating from unexpected processes (scaffolded for future cross-process thread owner detection)

**Evidence types**: `HandleToGameProcess`, `UnsignedModuleInGame`

**Files**:
- `AntiCheat.Core/Interfaces/IBehavioralMonitorService.cs` + `BehavioralEvidence` model
- `AntiCheat.Core/Services/BehavioralMonitorService.cs` — NtQuerySystemInformation + DuplicateHandle + EnumProcessModules + CreateToolhelp32Snapshot
- `AntiCheat.Detection/Detectors/BehavioralDetector.cs` — IDetector wrapper

**Status**: ✅ COMPLETE

---

## Layer 6 — Memory Scanner

**Purpose**: Deep in-process memory analysis to detect advanced evasion techniques.

**Subsystems**:
1. **Memory Region Scanning** — `VirtualQueryEx` loop from 0x0 to 0x7FFFFFFF; flags RWX private memory (shellcode), PE headers (`MZ` magic) in private memory (manual-map injection), RWX image memory (runtime code patching), executable guard pages (anti-debug), mass RWX allocations (>10% of address space), mass executable private pages
2. **Hidden Module Detection** — Compares `CreateToolhelp32Snapshot` (user-mode API) with PEB `InLoadOrderModuleList` (kernel-tracked via `ReadProcessMemory` at `PebBaseAddress+0x0C` → `Ldr → InLoadOrderModuleList.Flink`); any module present in PEB but absent from Toolhelp32 is a hidden/unlinked module
3. **Thread Analysis** — Enumerates game threads via `CreateToolhelp32Snapshot(TH32CS_SNAPTHREAD) + THREADENTRY32`; counts open threads, checks if threads in game belong to other processes
4. **Inline Hook Detection** — Reads function prologues from local process via `GetModuleHandle` + `GetProcAddress` (same address across processes on modern Windows due to system-wide KASLR); checks: JMP (0xE9), CALL (0xE8), JMP [indirect] (0xFF 0x25), PUSH/RET (0x68 ... 0xC3) at prologue of critical functions in ntdll.dll (NtOpenProcess, NtWriteVirtualMemory, NtCreateThreadEx, NtAllocateVirtualMemory, NtProtectVirtualMemory) and kernel32.dll (OpenProcess, WriteProcessMemory, CreateRemoteThread, VirtualAllocEx, VirtualProtectEx)

**Evidence types**: `RWX_Private_Memory`, `PE_Header_Private_Memory`, `RWX_Image_Memory`, `Guard_Page_Executable`, `Mass_RWX_Allocation`, `Mass_Executable_Private`, `NonStandard_Game_Module`, `Hidden_Module`, `Excessive_Thread_Count`, `Process_With_Game_Threads`, `Inine_Hook_Detected`

**Files**:
- `AntiCheat.Core/Interfaces/IMemoryScannerService.cs` + `MemoryEvidence` model
- `AntiCheat.Core/Services/MemoryScannerService.cs` — VirtualQueryEx loop, PEB hidden module detection, inline hook scanning, thread enumeration
- `AntiCheat.Detection/Detectors/AdvancedMemoryDetector.cs` — IDetector wrapper

**Status**: ✅ COMPLETE

---

## Layer 7 — Digital Certificate Reputation

**Purpose**: Online certificate verification. PE Analyzer extracts offline cert info — this layer validates it live.

**Checks**:
- CRL (Certificate Revocation List) / OCSP (Online Certificate Status Protocol) — `X509Chain.Build()` with online revocation
- Publisher reputation score — cached per-thumbprint in `CertificateReputation` table
- Certificate age / revocation / self-signed / chain-trusted — all scored
- 24hr cache in DB avoids repeated online lookups

**Files**:
- `AntiCheat.Core/Interfaces/ICertificateReputationService.cs` + `CertificateEvidence` model
- `AntiCheat.Core/Services/CertificateReputationService.cs` — scans game files, X509Chain.Build() with online CRL/OCSP, caches to DB
- `AntiCheat.Core/Data/Entities/CertificateReputationEntity.cs` — PK Thumbprint, verdict/reputation/seen counts
- `AntiCheat.Detection/Detectors/CertificateReputationDetector.cs` — IDetector, skips trusted signed files, flags revoked/expired/self-signed/untrusted
- `Migrations/AddCertificateReputation` — creates CertificateReputation table

**Status**: ✅ COMPLETE

---

## Layer 8 — ClamAV (Server-Side)

**Purpose**: Free signature-based AV as an additional signal. Self-hosted, no rate limits.

**How it works**:
- Installed on API server as a daemon (clamd) listening on TCP port 3310
- Client sends `INSTREAM` command with file data in chunks via `TcpClient`
- Response: `stream: OK` (clean) or `stream: VirusName` (infected)
- Results cached in `ClamAvResults` table by SHA256 (1hr memory cache, DB forever)
- Configurable via `ClamAvSettings` (host, port, timeout, max file size, enabled)
- Only enabled when `"Enabled": true` in config — no hard dependency

**Files**:
- `AntiCheat.Core/Configuration/ClamAvSettings.cs` — options class
- `AntiCheat.Core/Interfaces/IClamAvService.cs` + `ClamAvResult` model
- `AntiCheat.Core/Services/ClamAvService.cs` — TcpClient INSTREAM protocol, SHA256 caching, game directory scanner
- `AntiCheat.Core/Data/Entities/ClamAvResultEntity.cs` — PK Sha256, IsInfected, VirusName, ScanResult, timestamps
- `AntiCheat.Detection/Detectors/ClamAvDetector.cs` — IDetector, only flags infected results (0.85 confidence), skips clean/disabled
- `Migrations/AddClamAvResults` — creates ClamAvResults table

**Status**: ✅ COMPLETE

---

## Layer 9 — Team Cymru MHR

**Purpose**: Free hash reputation lookup against 30+ AV vendors. Unlimited via WHOIS/DNS.

**How it works**:
- Server queries `hash.cymru.com` on port 43 via raw TCP — send `<sha256>\r\n`, parse response `<detCount> <totalEngines> [<lastDetected>]`
- DetectionRate = detCount / totalEngines → classified as clean (0%), low (<5%), medium (5-20%), high (20-50%), critical (≥50%)
- Results cached in `TeamCymruResults` table by SHA256 (24hr memory + DB)
- Configurable via `TeamCymruSettings` (host, port, timeout, enabled)
- Only enabled when `"Enabled": true` — no hard dependency

**Files**:
- `AntiCheat.Core/Configuration/TeamCymruSettings.cs` — options class
- `AntiCheat.Core/Interfaces/ITeamCymruService.cs` + `TeamCymruResult` model
- `AntiCheat.Core/Services/TeamCymruService.cs` — TcpClient WHOIS protocol, SHA256 lookup, caching, game directory scanner
- `AntiCheat.Core/Data/Entities/TeamCymruResultEntity.cs` — PK Sha256, DetectionCount, TotalEngines, DetectionRate, ScanResult
- `AntiCheat.Detection/Detectors/TeamCymruDetector.cs` — IDetector, flags suspicious results with confidence 0.6-0.95 based on detection rate
- `Migrations/AddTeamCymruResults` — creates TeamCymruResults table

**Status**: ✅ COMPLETE

---

## Layer 10 — Community Reputation

**Purpose**: Automatic trust scoring based on network-wide telemetry. Built into Layer 1 schema.

**Logic**:
```
TimesSeen > 1000 AND TimesFlagged == 0 → Auto-trusted
TimesSeen > 100  AND FlagRate < 1%    → Trusted
TimesFlagged / TimesSeen > 10%         → Suspicious
TimesFlagged / TimesSeen > 50%         → Cheat
UniquePlayers with bans > 5            → Cheat
```

**Status**: ✅ BUILT INTO LAYER 1 (ConfidenceScore in ReputationService.ComputeConfidence)

---

## Layer 11 — Verdict Engine

**Purpose**: The final decision layer. Consumes structured evidence from all detectors and produces a single verdict with confidence score and suggested action.

**How it works**:
- `IVerdictService.EvaluateAsync()` is called at the end of every `DetectionEngine.RunFullScanAsync()` cycle
- Evidence is grouped by detector type, each group scored as `maxConfidence × detectorWeight`
- Detector weights: Memory Scanner/Injection Detector/Kernel Scanner (0.85-0.9), YARA/Behavioral (0.8-0.85), Game Integrity/Module Integrity (0.75), PE Analyzer/Process Analyzer (0.65-0.7), ClamAV (0.6), Team Cymru (0.55), Certificate Reputation (0.5)
- Normalized final confidence: `weightedSum / totalWeight`
- Frequency bonus: +0.05-0.15 if same player has repeat verdicts within 1 hour
- Verdict classification:
  - `confidence ≥ 0.85` or `≥2 critical` → **cheat** → **ban**
  - `confidence ≥ 0.65` or `≥2 high + medium` → **suspicious** → **flag**
  - `confidence ≥ 0.35` or `≥3 medium` → **suspicious** → **warn**
  - `confidence > 0` → **low_risk** → **none**
  - else → **clean** → **none**
- Auto-escalation when `≥3 events` and `≥2 are critical+high`
- All verdicts persisted to `VerdictHistory` table for audit trail
- `GetLastVerdictAsync()` retrieves the most recent verdict for a player

**Files**:
- `AntiCheat.Core/Interfaces/IVerdictService.cs` — interface
- `AntiCheat.Core/Services/VerdictService.cs` — weighted scoring, frequency bonus, verdict classification, persistence
- `AntiCheat.Core/Models/VerdictResult.cs` — result DTO with contributions
- `AntiCheat.Core/Data/Entities/VerdictEntity.cs` — PK Id, PlayerId, FinalConfidence, Verdict, SuggestedAction, ContributionsJson
- `AntiCheat.Core/Services/DetectionEngine.cs` — now calls `IVerdictService.EvaluateAsync()` after risk assessment
- `Migrations/AddVerdictHistory` — creates VerdictHistory table

**Status**: ✅ COMPLETE

---

## Layer 12 — Optional Sandbox Detonation

**Purpose**: Deep analysis of truly unknown files that pass all other layers.

**How it works**:
- Only triggered when evidence is inconclusive
- Client uploads file to server
- Server runs in Windows VM sandbox for 30s
- Watches: registry, files, network, injection, memory, threads, mutexes, services, drivers, DLLs
- Generates evidence report, cached forever

**Status**: 📅 FUTURE (not needed until scale)

---

## Build Order

| Phase | Layer | Status |
|-------|-------|--------|
| 1 | Internal Hash DB + Reputation API | ✅ |
| 2 | Game Integrity Verification | ✅ |
| 3 | Deep PE Analyzer | ✅ |
| 4 | YARA Rules | ✅ |
| 5 | Behavioral Monitor | ✅ |
| 6 | Memory Scanner | ✅ |
| 7 | Digital Certificate Reputation | ✅ |
| 8 | ClamAV | ✅ |
| 9 | Team Cymru MHR | ✅ |
| 10 | Community Reputation | ✅ Built-in |
| 11 | Verdict Engine | ✅ |
| 12 | Sandbox Detonation | ✅ |
