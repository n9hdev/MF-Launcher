# Changelog

## v6.0.0-rc1 (Release Candidate)

### Major Features
- Complete detection engine with 10 detectors (memory, injection, kernel, YARA, anti-injection, module integrity, memory region, timing analysis, anti-tamper)
- Real-time background scanning with adaptive interval (10–30s)
- Cross-detector correlation engine with multi-signal threat scoring
- Evidence pipeline with auto-capture at confidence ≥0.8
- Screenshot capture and live screen streaming via SignalR
- Role-based UI (player, moderator, admin, superadmin) with 30+ permissions
- Game launcher with MTA:SA integration and named pipe IPC
- Remote API integration for centralized reporting
- Windows Service deployment option
- JWT authentication with refresh token rotation
- Device fingerprint trust scoring
- 189 automated tests (88 backend, 101 frontend)

### Performance (Phase 15)
- JWT handler/token validation cached as static (zero per-call allocations)
- 6 database composite indexes eliminating N+1 query patterns
- ArrayPool buffer reuse for screen capture
- Frontend code splitting: vendor (163KB), motion (115KB), signalr (55KB), UI (33KB)
- 27 lazy-loaded routes with React.lazy() + Suspense
- esbuild minification, es2022 target, sourcemap disabled
- SignalR reconnect capped at 10, exponential backoff
- Detection engine LINQ and caching optimizations

### Production Hardening (Phase 15.5)
- JWT default secret detection — fails fast with meaningful error
- Remote API disabled by default (opt-in)
- Seed data only in Development environment
- CORS configurable via settings
- HTTPS redirection enabled
- Thread safety fixes across all singleton services
- Evidence store bounded at 10,000 entries
- Auto-cleanup of stale stream sessions (5 min interval)
- SignalR connection tracking cleaned on disconnect
- Correlation engine thread-safe with locking

### Security
- JWT dependency upgraded 7.0.0 → 8.19.1 (GHSA-59j7-ghrg-fj52 resolved)
- Hardcoded default JWT secret removed
- Hardcoded default API key removed
- Demo users gated behind Development environment only
- BCrypt password hashing

### Known Limitations
- 15 transitive npm vulnerabilities in electron-builder (not in app code)
- 6 pre-existing CA1416 Windows platform warnings (expected)
- Node.js v18.20.4 limits test environment to happy-dom
- No parallel detector execution (sequential for safety)
- No database migration strategy (EnsureCreated only)
- No API rate limiting

### Breaking Changes
- Remote API defaults to disabled (`Enabled: false`)
- JWT secret must be explicitly configured (startup fails with default)
- Seed users only available in Development environment
- appsettings.json defaults changed — review CONFIGURATION.md before upgrade

## v5.x (Legacy)
Previous versions. See LEGACY_ANALYSIS.md for details.
