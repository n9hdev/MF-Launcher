# Production Checklist — Mafia City Anti-Cheat V6

## Deployment Checklist

### Prerequisites
- [ ] .NET 8.0 Runtime installed on target machine
- [ ] MySQL 8.0+ server accessible with `mafia_security` database created
- [ ] Node.js 18+ (for electron-builder, runtime not required)
- [ ] Windows 10/11 or Windows Server 2019+ (target OS)

### Configuration (`appsettings.json`)
- [ ] **Jwt:Secret** — Set to a cryptographically random string ≥32 characters (do NOT use default)
- [ ] **Jwt:Issuer** — Set to application identity (e.g., `"AntiCheatV6"`)
- [ ] **Jwt:Audience** — Set to client identity (e.g., `"AntiCheatV6.Client"`)
- [ ] **ConnectionStrings:DefaultConnection** — Valid MySQL connection string to `mafia_security`
- [ ] **RemoteApi:Enabled** — Set `true` only if remote API server is available
- [ ] **RemoteApi:BaseUrl** — Remote API server URL (e.g., `http://10.147.20.39:9000`)
- [ ] **RemoteApi:ApiKey** — Remote API authentication key
- [ ] **Cors:AllowedOrigins** — Array of allowed origins (e.g., `["http://localhost:5173"]`). Leave empty for `AllowAnyOrigin` (development only)
- [ ] **Urls** — Bind address (e.g., `http://0.0.0.0:5000` or `https://0.0.0.0:5001`)
- [ ] **Kestrel HTTPS certificate** configured if using HTTPS

### Environment Variables (Override)
- `Jwt__Secret` — Overrides JWT secret (preferred over config file)
- `ConnectionStrings__DefaultConnection` — Overrides DB connection string

### Build Configuration
- [ ] Build in **Release** configuration: `dotnet publish -c Release`
- [ ] Frontend: `npm run build` generates `dist/renderer/` with code splitting
- [ ] Verify `ASPNETCORE_ENVIRONMENT=Production` is set (no debug endpoints, no seed data)
- [ ] Verify `RemoteApi.Enabled=false` if remote server is not deployed
- [ ] Verify no DEBUG compilation symbols

### Post-Deployment Verification
- [ ] API responds at configured URL
- [ ] JWT authentication works (login + token exchange)
- [ ] SignalR hub connects (`/hub/anticheat`, `/hub/screenstream`)
- [ ] Database tables created (via `EnsureCreated()`)
- [ ] Admin user registered with secure password (via registration endpoint)
- [ ] Background scan service running (logs show "Real-time detection background service started")
- [ ] Heartbeat service configured (if enabled)
- [ ] Frontend loads with correct API base URL

---

## Known Issues

### 1. Unresolved npm Vulnerabilities (15 transitive)
- All vulnerabilities are in `electron-builder` and `electron` packages (not application code)
- 3 moderate, 11 high, 1 critical — none exploitable through app code
- Resolution requires major version upgrade of electron (33.x → 34+/35+)

### 2. Node.js Version Constraint
- Current: v18.20.4
- Vitest tests use `happy-dom` because `jsdom` v28+ requires Node 20+
- No impact on production (build output is static files)

### 3. Windows Platform Warnings (6 CA1416)
- Pre-existing warnings in `KernelScanner` and `GameLauncherService`
- Anti-cheat targets Windows only — these are safe
- Warnings suppressed via project-level `NoWarn` if desired

### 4. No Parallel Detector Execution
- `DetectionEngine.RunFullScanAsync` runs detectors sequentially
- Parallel execution deferred — requires per-detector shared-state isolation review
- Sequential execution is safe; parallel would increase throughput at the cost of complexity

### 5. Sequential Detector Iteration Reuses the Same Process Snapshot
- Each detector calls its own `Process.GetProcesses()` — minor overhead
- Could be optimized with shared snapshot, but adds coupling risk

---

## Remaining Risks

### Medium Risk
- **Default JWT Secret in development**: appsettings.json still shows a placeholder. Production deployment MUST override this.
- **MySQL root user with empty password**: Default connection string uses `root` with no password. Production should use a dedicated database user with limited privileges.
- **CORS wide open in development**: AllowAnyOrigin is only active when `Cors:AllowedOrigins` is empty. Must be configured in production.
- **No rate limiting**: API endpoints have no request rate limiting. Consider adding middleware for production.

### Low Risk
- **No database migration strategy**: `EnsureCreated()` is used — fine for initial deployment but not for schema migrations.
- **Static in-memory stores**: Evidence, screenshot, and stream session stores are static and process-local. Data is lost on restart. Acceptable for current architecture.
- **No authentication on Electron frontend**: The frontend stores JWT tokens in Zustand (in-memory + localStorage). Token persistence is acceptable for desktop app UX.

### Accepted Risk
- **Sequential detector execution**: Safe for now. Parallelism can be added later with isolation guarantees.
- **Synchronous file writes in ScreenCaptureService.CaptureAsync**: Small I/O, negligible blocking.
- **No HSTS configured**: Acceptable for LAN/internal deployment. Enable for internet-facing deployments.

---

## Performance Summary (Phase 15)

| Metric | Value |
|---|---|
| Backend tests | 88/88 pass (0 failures) |
| Frontend tests | 101/101 pass (0 failures) |
| Backend build errors | 0 |
| Frontend build errors | 0 |
| NuGet vulnerabilities | 0 (JWT 8.19.1 resolves GHSA-59j7-ghrg-fj52) |
| Frontend bundle (vendor) | 163.75 KB (53.46 KB gzip) |
| Frontend bundle (total split) | ~500 KB uncompressed |
| Lazy-loaded routes | 27 |
| Code splitting chunks | 5 (vendor, motion, signalr, ui, index) |
| Adaptive scan interval | 10–30s (auto-adjusts based on correlation score) |
| In-memory capture cap | 10,000 entries (bulk trim 1,000) |
| Stream session timeout | 5 min (auto-cleanup every 5 min) |
| SignalR reconnect | Capped at 10 attempts, exponential backoff |

---

## Security Summary

### Authentication
- JWT with HMAC-SHA256 signing
- Configurable issuer, audience, expiration
- Refresh token rotation (revoke on use)
- BCrypt password hashing

### Authorization
- Role-based (player, moderator, admin, superadmin)
- Permission service with 30+ granular permissions
- Feature flag system (10 flags)
- All SignalR hubs role-gated with `[Authorize(Roles = "...")]`

### Data Protection
- Device fingerprint trust scoring
- Session management (list + terminate)
- Hardware ID (SHA256 from WMI)
- Screenshot HMAC signing

### Production Hardening (Phase 15.5)
- JWT default secret detection — startup fails if default detected
- Remote API disabled by default (opt-in)
- Seeded demo users only in Development environment
- CORS configurable via AllowedOrigins (falls back to AllowAnyOrigin only when empty)
- HTTPS redirection enabled
- No hardcoded API keys in production defaults
- Thread-safe singleton services (all shared state protected with locks)
- Evidence store capped at 10,000 with trim
- Stale stream sessions auto-cleaned every 5 minutes
- `_lastFrameTime` cleaned up on SignalR disconnect
- Correlation score reads/writes protected with lock
- Config validation on startup fails with meaningful error for missing JWT secret

---

## Rollback Plan

### Standard Rollback
1. Stop the API/Service: `net stop MafiaCityAntiCheatV6` (or kill process)
2. Replace binaries with previous known-good version
3. Restore previous `appsettings.json` (or keep current config if compatible)
4. Start the API/Service
5. Verify health endpoint responds and SignalR connects

### Database Rollback
- Current schema uses `EnsureCreated()` — no migration history
- To roll back DB changes: restore from backup, or drop and recreate `mafia_security` database
- User data (accounts, sessions, devices) stored in DB — backup before any upgrade

### Frontend Rollback
1. Replace `dist/renderer/` contents with previous build
2. Clear browser/Electron cache
3. Reload application

---

## Release Notes v6.0.0-rc1

### New Features
- Complete detection engine with 10 detectors (memory, injection, kernel, YARA, anti-injection, module integrity, memory region, timing analysis, anti-tamper)
- Real-time background scanning with adaptive interval (10–30s)
- Correlation engine with multi-signal threat scoring
- Evidence pipeline with auto-capture at confidence ≥0.8
- Screenshot capture and live screen streaming via SignalR
- Role-based UI (player, moderator, admin, superadmin) with 30+ permissions
- Game launcher with MTA:SA integration and named pipe IPC
- Remote API integration for centralized reporting
- Windows Service deployment option

### Performance Improvements (Phase 15)
- JWT handler/token validation cached as static (eliminates per-call allocations)
- 6 new database composite indexes eliminating N+1 query patterns
- ArrayPool buffer reuse for screen capture
- Frontend code splitting (vendor/motion/signalr/ui chunks)
- 27 lazy-loaded routes (no static page imports)
- esbuild minification, es2022 target, sourcemap disabled
- SignalR reconnect with exponential backoff (capped at 10)
- Detection engine LINQ and caching optimizations

### Production Hardening (Phase 15.5)
- JWT default secret detection — fails fast on startup with meaningful error
- Remote API disabled by default (opt-in via configuration)
- Seed data only runs in Development environment
- CORS configurable via settings (fallback to AllowAnyOrigin only when empty)
- HTTPS redirection
- Thread safety fixes across all singleton services
- Evidence store bounded at 10,000 entries
- Auto-cleanup of stale stream sessions (5 min interval)
- SignalR connection tracking cleaned on disconnect
- `volatile` removed, proper locking on shared state

### Security Fixes
- JWT dependency upgraded 7.0.0 → 8.19.1 (resolves GHSA-59j7-ghrg-fj52)
- Hardcoded default JWT secret removed from production defaults
- Hardcoded default API key removed
- Demo users gated behind Development environment only

### Known Issues
- 15 transitive npm vulnerabilities in electron-builder (not in app code)
- 6 pre-existing CA1416 Windows platform warnings
- Node.js v18.20.4 limits test environment to happy-dom
- No parallel detector execution (sequential for safety)
