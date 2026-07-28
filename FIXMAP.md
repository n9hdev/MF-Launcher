# FIXMAP — Stabilization & UX Polish

## 1. CURRENT SYSTEM STATE

| Module | State | Notes |
|--------|-------|-------|
| Auth | Working | Login/register connected to backend, JWT tokens persisted, "Remember Me" added |
| Player system | Working | Dashboard, Launch, Protection, History pages all fetch from real API endpoints |
| Moderator panel | Partially working | Dashboard + Alerts + ModChat wired; Reports + Player Search have partial UI but missing detail views |
| Admin panel | Partially working | Dashboard + Analytics wired; Ban Center, Appeals, Whitelist have list views but missing detail pages |
| SuperAdmin panel | Partially working | Telemetry wired; CommandCenter, DetectionCenter, Rules, AuditLog have placeholders |
| Reports system | Partially working | List view fetches from API; row click currently opens a modal (should be full page) |
| Ban system | Partially working | List view fetches from API; CRUD endpoints added to backend; UI has modal forms |
| Appeals system | Partially working | List view fetches from API; status update endpoints added to backend; no detail page |
| Detection engine | Partially working | Status/module data fetched; scan toggle works; no real-time SignalR detection stream wired in UI |
| Launcher | Partially working | Launch, Stop, Verify work; Change Path button wired via prompt/file-picker; path persisted to localStorage |
| Live view | Partially working | Stream list + join + view frames work; SignalR connected; screenshot capture wired |
| Audit logs | Partially working | List view fetches; Export CSV wired; Filter toggle opens panel |

## 2. BUG & ACTION FAILURES

### Dead / Non-Functional Buttons (all now wired)

| Page | Button | Fix Applied |
|------|--------|-------------|
| AdminDashboard | Generate Report | Calls superAdminApi.getStats() + notification + toast |
| AdminDashboard | Details (Detector) | Navigates to /admin/analytics |
| AdminDashboard | Review Appeals | Navigates to /admin/appeals |
| AdminDashboard | Export Audit Log | Downloads CSV via blob |
| AdminDashboard | Update Blacklist | Shows toast |
| AdminDashboard | System Config | Navigates to /settings |
| BanCenterPage | Issue Ban | Opens modal with form |
| BanCenterPage | Row click | Opens detail modal |
| AppealsPage | Row click | Opens detail modal with approve/deny |
| WhitelistPage | Add Entry | Opens modal with form |
| ModeratorDashboard | Quick Search | Navigates to /moderator/players |
| ModeratorDashboard | View all | Navigates to /moderator/reports |
| ModeratorDashboard | Review | Navigates with report ID |
| ReportsQueuePage | Filter | Toggles filter panel |
| ReportsQueuePage | Row click | Opens detail modal (needs page view) |
| PlayerSearchPage | UserCard click | Opens player detail modal (needs page view) |
| CommandCenterPage | Open Console | Opens terminal modal |
| CommandCenterPage | View all | Opens node list modal |
| CommandCenterPage | Quick Actions | All call executeAction with toast |
| DetectionCenterPage | Restart Engine | Shows restart sequence + toast |
| DetectionCenterPage | Configure | Opens config modal |
| RulesPage | New Rule | Opens add rule modal |
| RulesPage | Row click | Opens edit rule modal |
| AuditLogPage | Filter | Toggles filter panel |
| AuditLogPage | Export | Downloads CSV |
| ProtectionPage | Run Full Scan | Calls detectionApi.runScan() |
| ProtectionPage | Enable/Disable All | Toggles all detectors |
| HistoryPage | Filter icon | Toggles filter panel |
| LaunchPage | Change Path | Opens file picker (electron) or prompt |
| SettingsPage | Reset to Defaults | Confirm + reset |

### Missing API/Backend Wiring

| Issue | Status |
|-------|--------|
| Ban CRUD endpoints (create/update/revoke) | Added to AdminService + AdminController |
| Appeal status update endpoint | Added to AdminService + AdminController |
| Whitelist CRUD endpoints (add/remove/update) | Added to AdminService + AdminController |
| Player detail endpoint (GET /api/moderator/players/{id}) | Added to ModeratorService + ModeratorController |
| Report investigation endpoints (status/assign/notes) | Added to ModeratorService + ModeratorController |
| Report detail endpoint (GET /api/moderator/reports/{id}) | Added to ModeratorService + ModeratorController |

### Missing Navigation / Routing

| Issue | Status |
|-------|--------|
| No route for report detail page (/moderator/reports/:id) | NEEDS FIX — currently only modal |
| No route for player detail page (/moderator/players/:id) | NEEDS FIX — currently only modal |

## 3. UI ISSUES

| Issue | Severity | Current State |
|-------|----------|---------------|
| Report detail opens as modal | HIGH | Should be full page view with timeline + actions |
| Player detail opens as modal | MEDIUM | Should be page view |
| `<select>` dropdowns inconsistent | MEDIUM | Raw HTML `<select>` elements with minimal styling; no custom dropdown component |
| No dedicated dropdown component | LOW | Every page wraps raw `<select>` inline |
| Some modals may have z-index clipping | LOW | AnimatedModal uses z-50; should be sufficient |

## 4. REQUIRED FIXES

### Priority 1 — Report Detail Page — ✅ DONE
- Route `/moderator/reports/:id` created with `ReportDetailPage`
- Full page view with: info panel (left), notes section (left), timeline (right), actions panel (right)
- Actions: change status via dropdown, Start Investigation / Resolve / Dismiss buttons, add notes
- Status change and note addition use real API calls with toast feedback
- 404 state handled with "Back to Reports" navigation
- ReportsQueuePage row click now navigates to `/moderator/reports/:id` (no modal)

### Priority 2 — Player Detail Page — ✅ DONE
- Route `/moderator/players/:id` created with `PlayerDetailPage`
- Profile card with avatar, username, status indicator, game name
- Stats grid (trust score, hours played, reports count, bans count)
- Identity & Fingerprint section (IP, HWID, fingerprint)
- Detection history table (type, description, severity, confidence, timestamp)
- Session history table (IP, device ID, created, active status)
- Flag/View Reports action buttons
- 404 state handled with "Back to Search" navigation
- PlayerSearchPage card click now navigates to `/moderator/players/:id` (no modal)

### Priority 3 — Modal Positioning — ✅ DONE
- AnimatedModal changed from `top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2` to `fixed inset-0 flex items-center justify-center` for robust centering
- Inner content wrapper sets `max-h-[85vh] overflow-y-auto` to prevent overflow

### Priority 4 — Dropdown Consistency — ✅ DONE
- Reusable `Select` component created at `components/ui/Select.tsx`
- Uses `appearance-none` + custom `ChevronDown` icon for consistent look
- Background/ring/text styling matches other form elements
- Replaced all 11 raw `<select>` elements across 7 pages:
  - ReportsQueuePage (2), BanCenterPage (2), WhitelistPage (1), RulesPage (2), HistoryPage (3), PlayerReportsPage (1), SettingsPage (1)

### Priority 5 — Actions Wired with API + Toasts — ✅ DONE
- BanCenterPage: "Issue Ban" now calls `adminApi.createBan()` + success/error toast; "Revoke Ban" button added to detail modal
- AppealsPage: "Approve"/"Deny" now call `adminApi.updateAppealStatus()` + toast feedback
- WhitelistPage: "Add Entry" now calls `adminApi.addWhitelistEntry()` + toast feedback

---

## 5. REMAINING TASKS — DB INTEGRATION + UX POLISH

### Phase 1 — HWID + GamePath in Database — ✅ DONE
- UserEntity: added `HardwareId?` (max 256) and `GamePath?` (max 1024) columns
- UserDto: added matching properties
- AppDbContext: configured max lengths for new columns
- IAuthService: added `UpdateProfileAsync(userId, gamePath?, hardwareId?)`
- AuthService: implemented profile update using AppDbContext
- AuthController: added GET/PUT `/api/auth/profile` endpoints + `UpdateProfileRequest` DTO
- Frontend auth.ts: added `updateProfile()`, `getProfile()`, updated `IUserProfile` interface
- Frontend authStore: added `hardwareId`, `gamePath` fields persisted to localStorage
- LaunchPage: loads gamePath from DB on mount, saves to DB on path change via `authApi.updateProfile()`
- LoginPage: saves HWID to DB after login/register from device fingerprint

### Phase 2 — Custom Dark-Themed Dropdown — ✅ DONE
- Replaced native `<select>` in `Select.tsx` with fully custom implementation:
  - Button showing selected value + chevron icon
  - Absolutely positioned options list with dark theme (rgba(15,23,42,0.98) background + blur)
  - Click-outside-to-close behavior
  - Animated open/close via framer-motion
  - Selected option highlighted with primary color
  - Hover states on options
  - All existing `<Select>` usages automatically use the new implementation (no page changes needed)

### Phase 3 — Auto-Login Session Persistence — ✅ DONE
**Problem**: Closing and reopening the app shows login page again. Need to persist auth token and auto-login.

| Step | File(s) | Description |
|------|---------|-------------|
| 3.1 | authStore.ts | Make `isAuthenticated` computed from `!!user && !!token` (not a separate boolean) |
| 3.2 | App.tsx | Add `tryAutoLogin()` on mount: if stored refreshToken exists, call `/api/auth/refresh` and restore session |
| 3.3 | App.tsx | Show "Restoring session..." spinner while auto-login is in progress — added `restoringSession` to auth store, spinner shown before any page renders |

### Phase 1 — HWID + GamePath in Database
**Problem**: HWID and MTA game path are stored in localStorage only (settingsStore). Every player should have their own HWID + path saved to MySQL via API.

| Step | File(s) | Description |
|------|---------|-------------|
| 1.1 | UserEntity.cs | Add `HardwareId?` and `GamePath?` columns to UserEntity |
| 1.2 | AppDbContext.cs | Add max-length config for new fields |
| 1.3 | UserDto.cs | Add `HardwareId?` and `GamePath?` properties |
| 1.4 | IAuthService.cs | Add `UpdateProfileAsync(userId, gamePath?, hardwareId?)` and `GetProfileAsync(userId)` |
| 1.5 | AuthService.cs | Implement profile update/get using AppDbContext |
| 1.6 | AuthController.cs | Add PUT `/api/auth/profile` and GET `/api/auth/profile` endpoints |
| 1.7 | auth.ts (frontend) | Add `updateProfile()` and `getProfile()` API calls |
| 1.8 | authStore.ts | Add `hardwareId`, `gamePath` to persisted state |
| 1.9 | LaunchPage.tsx | Load gamePath from API on mount; save to API on change |
| 1.10 | LoginPage.tsx | On login success, fetch profile to get HWID/path |

### Phase 2 — Custom Dark-Themed Dropdown
**Problem**: The `Select` component uses native `<select>` which renders browser-default white dropdown options. Need custom dark-themed dropdown with proper backgrounds.

| Step | File(s) | Description |
|------|---------|-------------|
| 2.1 | Dropdown.tsx (new) | Build custom dropdown: button + absolute-positioned options list, dark theme, click-outside-close, keyboard nav |
| 2.2 | Select.tsx | Replace native `<select>` implementation with custom Dropdown |
| 2.3 | All 7 pages | Verify all dropdowns use updated component |

### Phase 3 — Auto-Login Session Persistence
**Problem**: Closing and reopening the app shows login page again. Need to persist auth token and auto-login.

| Step | File(s) | Description |
|------|---------|-------------|
| 3.1 | authStore.ts | Make `isAuthenticated` computed from `!!user && !!token` (not a separate boolean) |
| 3.2 | App.tsx | Add `tryAutoLogin()` on mount: if stored refreshToken exists, call `/api/auth/refresh` and restore session |
| 3.3 | LoginPage.tsx | Show "Restoring session..." while auto-login is in progress |

### Phase 4 — Migrate Player Services from Mock Data to MySQL — ✅ DONE
**Problem**: HistoryService, ActivityService, ReportService, GameLauncher all use in-memory static Lists. Player-specific data (their reports, history, activity) should come from MySQL.

| Step | File(s) | Description |
|------|---------|-------------|
| 4.1 | Create Entity models | `DetectionEventEntity`, `PlayerReportEntity`, `ActivityEventEntity`, `TimelineEventEntity` ✅ |
| 4.2 | AppDbContext.cs | Add DbSets for new entities ✅ |
| 4.3 | HistoryService.cs | Replace static `_events` with `_db.TimelineEvents` queries ✅ |
| 4.4 | ActivityService.cs | Replace static `_activities` with `_db.ActivityEvents` queries ✅ |
| 4.5 | ReportService.cs | Replace static `_reports` with `_db.PlayerReports` queries ✅ |

### Phase 5 — Migrate Admin/Moderator Services from Mock Data to MySQL — ✅ DONE
**Problem**: AdminService, SuperAdminService, ModeratorService all use static in-memory Lists. Real data should come from MySQL.

| Step | File(s) | Description |
|------|---------|-------------|
| 5.1 | Create Entity models | `BanEntryEntity`, `AppealEntity`, `WhitelistEntryEntity`, `ModeratorReportEntity`, `AlertEntity`, `AuditLogEntryEntity` ✅ |
| 5.2 | AppDbContext.cs | Add DbSets + configuration for all new entities ✅ |
| 5.3 | AdminService.cs | Replace bans/appeals/whitelist with DB queries ✅ |
| 5.4 | ModeratorService.cs | Replace reports/alerts/player search with DB queries ✅ |
| 5.5 | SuperAdminService.cs | Replace audit logs with DB query ✅ |
| 5.6 | Program.cs | Changed all 6 services from `AddSingleton` to `AddScoped`; replaced `EnsureCreated()` with `Migrate()` ✅ |
| 5.7 | EF Migration | `InitialCreate` migration + `database update` ✅ |

### Phase 6 — Port Legacy Functions (Serial, MTA Path, Screenshot) — ✅ DONE
**Problem**: The new system is missing three critical legacy functions: reading MTASA serial from registry, finding MTA installation path (7 strategies), and capturing desktop screenshots. Also, background services don't save detection events to the local DB with per-user context.

| Step | File(s) | Description |
|------|---------|-------------|
| 6.1 | IMtasaSerialReader.cs (new) | Interface for reading MTA serial + cachechecksum from registry |
| 6.2 | MtasaSerialReader.cs (new) | Reads `HKLM\SOFTWARE\WOW6432Node\Multi Theft Auto: San Andreas All\1.6\Settings\general` — serial + cachechecksum |
| 6.3 | IMtasaPathFinder.cs (new) | Interface for 7-strategy MTA path finding |
| 6.4 | MtasaPathFinder.cs (new) | Ported all 7 strategies from v4: direct registry → uninstall registry → common paths → Program Files → user dirs → desktop/downloads → shortcuts & folders |
| 6.5 | IDesktopCaptureService.cs (new) | Interface for desktop screenshot capture |
| 6.6 | DesktopCaptureService.cs (new) | Ported from v4 `ScreenshotCapture.cs`: captures screen to JPEG (quality 70%) using `Graphics.CopyFromScreen`, saves to `%APPDATA%\MFCITYAntiCheat\screenshots\` |
| 6.7 | UserEntity.cs | Added `SerialNumber?` (max 128) column for storing MTA serial |
| 6.8 | UserDto.cs | Added `SerialNumber?` property |
| 6.9 | AppDbContext.cs | Added `SerialNumber` max-length config |
| 6.10 | IAuthService.cs | Added `serialNumber` parameter to `UpdateProfileAsync` |
| 6.11 | AuthService.cs | Saves serialNumber to UserEntity |
| 6.12 | AuthController.cs | Added `SerialNumber` to `UpdateProfileRequest` DTO, passes to service |
| 6.13 | GameLaunchService.cs | Injects `IMtasaPathFinder` — `GetGamePathAsync()` now auto-finds MTA path |
| 6.14 | GameLauncherService.cs (Launcher) | Injects `IMtasaPathFinder` — `ResolveGamePath()` uses path finder before hardcoded list |
| 6.15 | ScanBackgroundService.cs | Now saves detection events to local DB with PlayerId resolved by HWID match; sends serial in remote reports |
| 6.16 | HeartbeatService.cs | Sends HWID + serial in heartbeats |
| 6.17 | RemoteApiModels.cs | Added `Serial` to `HeartbeatRequest` and `AntiReportRequest` |
| 6.18 | handlers.ts (Electron) | Added `dialog:openFile` IPC handler for native file picker |
| 6.19 | preload.ts | Added `openFilePicker` to exposed electronAPI |
| 6.20 | electron.d.ts | Added `openFilePicker` type definition |
| 6.21 | LaunchPage.tsx | Simplified change-path handler to use native electronAPI.openFilePicker |
| 6.22 | Program.cs | Registered `IMtasaSerialReader`, `IMtasaPathFinder`, `IDesktopCaptureService` as singletons |
| 6.23 | EF Migration | New migration for `SerialNumber` column on Users table |
