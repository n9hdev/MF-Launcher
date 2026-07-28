# Anti-Cheat Platform — Frontend + Backend Feature Roadmap

## Architecture Philosophy
- **Single-PC binding**: One user = one hardware identity. Ban bypass via account hopping made impossible.
- **Evidence-driven bans**: Every ban has proof (screenshot → Cloudinary → stored URL).
- **Real-time by default**: All state changes (trust, ban, scan) push instantly via SignalR — no polling.
- **Trust before launch**: Game cannot launch until HWID is verified and player is `Trusted`.

```
Login ──► HWID Check ──► Trust Assessment ──► Dashboard (locked) ──► HWID Scan ──► Trusted? ──► Launch Unlocked
              │                                                           │
              ▼                                                           ▼
         Block if HWID mismatch                                     Untrusted → Locked
```

---

## Phase 1 — HWID / Device Binding (Backend Enforcement)

**Goal**: Prevent ban bypass by binding each account to a single hardware identity.

### Requirements
- On **register**: If the HWID is already linked to another user → ❌ Block registration
- On **login**: If the user's stored HWID doesn't match current HWID → ❌ Block access
- On **profile update**: Re-binding allowed only if no HWID was previously stored
- HWID collected automatically via `HardwareIdProvider` (CPU + GPU + Disk → SHA256)
- MTA serial collected from registry as secondary binding

### Backend changes
- `AuthService.RegisterAsync` — check HWID uniqueness before creating user
- `AuthService.LoginAsync` — verify HWID matches stored value (if HardwareId is set)
- New endpoint: `POST /api/auth/verify-hardware` — verify HWID without login
- `AuthService.CollectAndSaveHardwareInfo` — always updates game path + serial; HWID only set if previously null

### Frontend changes
- `LoginPage.tsx` — show HWID error message with "Contact Support" if blocked
- `authStore.ts` — add `hwidVerified: boolean` field
- On login success, device identity is sent and validated

**Status**: ⬜ NOT STARTED

---

## Phase 2 — Player Trust Status System

**Goal**: Show `Trusted` / `Untrusted` status indicator and gate game launch behind it.

### Trust Logic
- `Trusted` = HWID is verified + TrustScore ≥ 50 + user is not banned
- `Untrusted` = HWID scan not completed OR TrustScore < 50
- Computed server-side on login, pushed via SignalR on status changes

### Backend changes
- Add `TrustStatus` property to user profile endpoint response
- Add `GET /api/auth/trust-status` endpoint
- `UserEntity.Status` value `"banned"` checked in trust calculation

### Frontend changes
- `global.d.ts` — add `ITrustStatus { isTrusted: boolean; trustedAt?: string; status: 'trusted' | 'untrusted' | 'pending' }`
- `authStore.ts` — add `trustStatus` field
- `FloatingTopBar.tsx` — show Trusted/Untrusted badge next to user name
- `PlayerDashboard.tsx` — show trust status in metrics area
- Mod/admin player detail view — show trust status

**Status**: ⬜ NOT STARTED

---

## Phase 3 — Game Launch Lock System (Frontend Protection)

**Goal**: Both launch buttons remain locked until HWID scan completes and status is `Trusted`.

### Lock Logic
- Default state: **LOCKED** (lock icon overlay, pointer-events: none)
- Unlock when BOTH conditions met:
  1. HWID scan completed (`authStore.hwidVerified === true`)
  2. Trust status is `Trusted` (`authStore.trustStatus.status === 'trusted'`)

### Frontend changes
- `PlayerDashboard.tsx` — "Launch MTA:SA" button gets lock overlay + disabled state
- `LaunchPage.tsx` — Large launch button gets lock overlay + disabled state
- `AnimatedButton.tsx` — add `locked` prop that shows lock icon overlay
- Show tooltip explaining why button is locked (e.g., "Complete HWID scan to unlock")
- On unlock transition: play unlock animation (framer-motion)

**Status**: ⬜ NOT STARTED

---

## Phase 4 — Banned User View (Dedicated Screen)

**Goal**: Banned users see a restricted view showing ban details + appeal link — no access to dashboard.

### Ban Details Display
- Ban duration (permanent / X hours remaining)
- Ban reason
- Proof image from Cloudinary (if available)
- Issued by / Issued at
- Appeal button (opens appeal form)
- Contact support link

### Backend changes
- `BanEntryEntity` — add fields: `PlayerId`, `SerialNumber`, `IpAddress`, `ProofUrl`, `BannedAt`, `DurationHours`
- Migration for new BanEntryEntity fields
- `BanService.GetActiveBanAsync(playerId)` — returns active ban with full details
- `GET /api/ban/my` — current user gets their own ban details (no auth role required beyond authenticated)
- `POST /api/ban/appeal` — appeal endpoint for banned users
- `AuthService.LoginAsync` — if user status is `"banned"`, return ban info in login response

### Frontend changes
- `App.tsx` — after login, check if user is banned; redirect to `/banned`
- `BannedPage.tsx` — dedicated page showing:
  - Ban banner (red, cannot be dismissed)
  - Ban details card (reason, duration, proof image)
  - Appeal form (text area + submit)
  - Contact support button
  - No sidebar navigation (restricted view)
- `ProtectedRoute.tsx` — exclude banned users from normal routes
- `authStore.ts` — add `isBanned`, `banInfo` fields
- `global.d.ts` — add `IBanInfo` interface

**Status**: ⬜ NOT STARTED

---

## Phase 5 — Screenshot System (Cloudinary Integration)

**Goal**: Screenshots captured from client (desktop) are sent to API → uploaded to Cloudinary → stored as proof.

### Flow
```
Client (Electron) ──► API (ScreenCaptureController) ──► Cloudinary ──► URL stored in DB / BanEntry
```

### Backend changes
- `ScreenshotProofEntity` — stores Cloudinary URL, playerId, detectionEventId, capturedAt
- `IScreenshotProofService` + `ScreenshotProofService`
  - `UploadToCloudinaryAsync(base64Image)` → returns URL
  - `SaveProofAsync(playerId, eventId, cloudinaryUrl)`
- `ScreenCaptureController` — new endpoint `POST /api/screen/upload-proof`
  - Accepts: `{ playerId, detectionEventId, image (base64) }`
  - Uploads to Cloudinary
  - Stores proof record in DB
  - Returns Cloudinary URL
- `appsettings.json` — add `Cloudinary:CloudName`, `Cloudinary:ApiKey`, `Cloudinary:ApiSecret`
- Cloudinary NuGet package: `CloudinaryDotNet`

### Frontend changes
- `DesktopCaptureService.cs` — capture actual screenshot via `Graphics.CopyFromScreen`
- `screenCapture.ts` — `uploadProof(playerId, eventId, base64Image)` method
- Auto-capture on detection events with confidence ≥ 0.8 (fires through DetectionEngine → SignalR → frontend captures)
- Ban proof images loaded from Cloudinary URL

**Status**: ⬜ NOT STARTED

---

## Phase 6 — Auto Ban System

**Goal**: On detection with high confidence → auto-ban with serial, IP, proof screenshot.

### Ban Trigger
- DetectionEngine generates event with confidence ≥ 0.85
- OR VerdictService returns `cheat` verdict
- AutoBanService is called:
  1. Captures screenshot (triggers Phase 5 flow)
  2. Creates BanEntry with: reason, serial (from user profile), IP (from session), proof URL (from Cloudinary)
  3. Sets user Status to `"banned"`
  4. Pushes ban event via SignalR (frontend immediately shows BannedPage)
  5. Logs to audit trail

### Backend changes
- `AutoBanService` (or integrate into `BanService`):
  - `AutoBanForPlayer(playerId, reason, detectionEventId)` — orchestrates the full flow
- `DetectionEngine.RunFullScanAsync()` — after VerdictService evaluate, if verdict is `cheat`/`ban`, call AutoBanService
- `ScanBackgroundService` — on detection event with confidence >= 0.85, trigger auto-ban
- Ban includes: playerId, reason, serialNumber (from UserEntity), ipAddress (from Session), proofUrl (from ScreenshotProofService)

**Status**: ⬜ NOT STARTED

---

## Phase 7 — Real-Time Frontend Sync (SignalR Extension)

**Goal**: All state changes reflect instantly in frontend with no delay or manual refresh.

### Real-Time Events
| Event | Trigger | Frontend Action |
|-------|---------|-----------------|
| `BanStatusChanged` | Auto-ban / manual ban | Show BannedPage immediately |
| `TrustStatusChanged` | HWID scan completes | Update trust badge, unlock launch buttons |
| `ScanCompleted` | DetectionEngine finishes scan | Update metrics, show new events |
| `HwidVerified` | HWID scan successful | Update lock state |

### Backend changes
- `AntiCheatHub` — add new SignalR methods:
  - `SendBanStatus(userId, banInfo)` — pushes ban to specific user
  - `SendTrustStatus(userId, trustStatus)` — pushes trust update
  - `NotifyHwidVerified(userId)` — pushes HWID scan complete
- `BanService.AutoBanAsync` — calls `SendBanStatus` via `IHubContext<AntiCheatHub>`
- After HWID scan in `AuthService` — calls `NotifyHwidVerified` via hub context

### Frontend changes
- `signalr.ts` — add listeners:
  - `onBanStatus` — calls `authStore.setBanned(banInfo)` → triggers redirect
  - `onTrustStatus` — calls `authStore.setTrustStatus(status)`
  - `onHwidVerified` — calls `authStore.setHwidVerified(true)`
- `authStore.ts` — add actions: `setBanned`, `setTrustStatus`, `setHwidVerified`

**Status**: ⬜ NOT STARTED

---

## Build Order

| Phase | Feature | Depends On | Status |
|-------|---------|------------|--------|
| 1 | HWID / Device Binding | — | ⬜ |
| 2 | Player Trust Status System | Phase 1 | ⬜ |
| 3 | Game Launch Lock System | Phase 2 | ⬜ |
| 4 | Banned User View | — | ⬜ |
| 5 | Screenshot System (Cloudinary) | — | ⬜ |
| 6 | Auto Ban System | Phases 4, 5 | ⬜ |
| 7 | Real-Time Frontend Sync | Phases 1–6 | ⬜ |

---

## Key Design Decisions
- **Cloudinary** for proof image hosting (existing credentials reused from legacy system: `dokg4obtm`, key `598975739116563`, secret `cBQgTVJ2tGb2-YRIFVV7QCkmoyM`)
- **SignalR** already exists for real-time — extend, don't replace
- **BanEntryEntity** extended with new FK fields rather than creating separate table
- **Hardware binding** enforced at login — not retroactively; existing users with no stored HWID are prompted to bind on next login
- **Auto-ban** uses `BackgroundService` to decouple from detection pipeline (non-blocking)
- **Launch lock** is purely frontend (driven by backend trust status) — no backend enforcement needed for launch gating
