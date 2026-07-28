# Mafia City Anti-Cheat V6 — Production Installer

This folder builds a **single Windows installer** that deploys, on each player PC:

1. **The Dashboard** — the Electron desktop app (talks to your API server over HTTP).
2. **The Background Service** — `AntiCheat.Service`, registered as the Windows Service
   `MafiaCityAntiCheatV6` (auto-start, auto-restart on failure).

The API server itself runs on **your** PC (`10.147.20.39`), not in this installer.

---

## Architecture / network

```
   Player PC (installer target)                 Your PC  (10.147.20.39)
 ┌──────────────────────────────┐            ┌───────────────────────────┐
 │  Dashboard (Electron)  ──HTTP──────────►  │  AntiCheat.Api  :5000     │
 │  → http://10.147.20.39:5000  │  SignalR  │  (listens on 0.0.0.0:5000)│
 │                              │            │                           │
 │  AntiCheat.Service  ──HTTP──────────►     │  MySQL (server-only)      │
 │  (Windows Service)    POST   │            │  :3306 (NOT exposed)      │
 │  scans / heartbeats          │            │                           │
 │  ← commands back             │            │  API key auth:            │
 │                              │            │  X-Api-Key header         │
 └──────────────────────────────┘            └───────────────────────────┘
```

### Key changes from v5 (direct MySQL access)

| Old (v5) | New (v6) |
|-----------|----------|
| Service connects directly to MySQL (`3306` exposed to every player PC) | Service talks to the API via HTTP only |
| MySQL credentials baked into service binary (security risk) | API key auth via `X-Api-Key` header |
| Two separate DetectionEngine instances (service + api, double CPU) | Single DetectionEngine in the service, API handles persistence + verdicts |
| Service scan results only logged, never saved | Service POSTs scan results → API saves to DB + SignalR to dashboard |

### Flow: pre-launch scan → game unlock

```
1. Service starts (Windows boot)
   ├─ Runs pre-launch scan on the PLAYER'S PC (processes, memory, YARA, etc.)
   └─ POSTs results to /api/service/detections?scanType=prelaunch

2. API caches results per HWID (5 min TTL)

3. Player opens Dashboard → logs in → calls RequestPreLaunchScan via SignalR

4. API checks cache:
   ├─ Results found + clean → GameLaunchUnlocked (player can launch game)
   ├─ Results found + threats → PreLaunchResults (game blocked)
   └─ No results yet → PreLaunchPending (Dashboard shows "scanning" state)
       └─ Service gets "run_prelaunch_scan" command in next heartbeat response

5. Service runs continuous scan loop → POSTs detections in real-time
   ├─ API evaluates verdict → auto-bans if confidence threshold met
   └─ API pushes SignalR to Dashboard (live status updates)
```

### API endpoints used by the Service

| Method | Path | Purpose |
|--------|------|---------|
| POST | `/api/service/heartbeat` | Service reports online status every 5s; API returns commands (e.g. `run_prelaunch_scan`) |
| POST | `/api/service/detections` | Service sends scan results (prelaunch or continuous); API saves + evaluates verdicts |

All requests require the `X-Api-Key` header matching a key in the API's `ServiceApiKeys` config.

---

## Configuration before building

### 1. Generate a shared API key
Run this and use the output in both places below:
```powershell
# Generate a 32-char random key
-fjoin ([char[]](48..57+65..90+97..122) | Get-Random -Count 32 | % {[char]$_})
```

### 2. Set the API key in `src/backend/AntiCheat.Service/appsettings.json`
```json
{
  "ServiceApi": {
    "BaseUrl": "http://10.147.20.39:5000",
    "ApiKey": "YOUR_GENERATED_KEY_HERE"
  }
}
```

### 3. Set the SAME key in `src/backend/AntiCheat.Api/appsettings.json`
```json
"ServiceApiKeys": [
  "YOUR_GENERATED_KEY_HERE"
]
```

You can add multiple keys (one per different service build/group).

---

## Server-side prerequisites (do once on your PC — 10.147.20.39)

1. **Run the API** so it listens on all interfaces:
   ```powershell
   cd publish        # or dotnet publish the Api project
   .\AntiCheat.Api.exe
   # it now listens on http://0.0.0.0:5000
   ```

2. **Allow port 5000 through Windows Firewall** (only port the service/dashboard need):
   ```powershell
   New-NetFirewallRule -DisplayName "AntiCheat API 5000" -Direction Inbound -Protocol TCP -LocalPort 5000 -Action Allow
   ```

3. **MySQL stays local** — no remote MySQL user needed. Only the API (running on the same machine) connects to MySQL. Player PCs never touch the database directly.

---

## Building the installer

### 1. Install Inno Setup
Download from https://jrsoftware.org/isdl.php and note the path to `ISCC.exe`
(usually `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`).

### 2. Stage the files (publishes service + builds dashboard)
```powershell
pwsh -File scripts\build-installer.ps1
# optionally: -ServerIp 10.147.20.39 -Configuration Release
```
This produces:
- `installer\staging\service\`   — self-contained service (no .NET needed on player PC)
- `installer\staging\dashboard\` — Electron app (unpacked)

### 3. Compile the installer
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\MafiaCityAntiCheat.iss
```

### Result
```
installer\output\MafiaCityAntiCheat-Setup.exe
```
Give this single `.exe` to players.

---

## What the installer does on the player PC

- Installs the dashboard to `C:\Program Files\Mafia City Anti-Cheat V6\Dashboard`
- Installs the service to  `C:\Program Files\Mafia City Anti-Cheat V6\Service`
- Registers the Windows Service `MafiaCityAntiCheatV6` (auto-start + restart-on-failure) and starts it
- Creates Start-menu (and optional desktop) shortcuts for the dashboard
- Requires admin rights (needed to register a service)

## Uninstalling
Uninstalling stops and deletes the `MafiaCityAntiCheatV6` service, then removes all files.

---

## Notes / knobs

| What | Where |
|------|-------|
| Server IP for dashboard | `scripts\build-installer.ps1 -ServerIp ...` (rewrites `src/frontend/.env`) |
| Service → API base URL | `src/backend/AntiCheat.Service/appsettings.json` → `ServiceApi:BaseUrl` |
| Service → API key | `src/backend/AntiCheat.Service/appsettings.json` → `ServiceApi:ApiKey` |
| API → accepted service keys | `src/backend/AntiCheat.Api/appsettings.json` → `ServiceApiKeys` |
| API listen address | `src/backend/AntiCheat.Api/appsettings.json` → `Urls` |
| Service name | `MafiaCityAntiCheatV6` (in `.iss` and `AntiCheat.Service/Program.cs`) |
| Installer version | `MyAppVersion` in `installer\MafiaCityAntiCheat.iss` |
