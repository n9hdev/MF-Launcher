# Security

## Authentication

- JWT (JSON Web Tokens) with HMAC-SHA256 signing
- BCrypt password hashing (work factor: default)
- Refresh token rotation (old token revoked on use)
- Device fingerprint trust scoring
- Session management (list + terminate)

## Authorization

- Role-based: player, moderator, admin, superadmin
- Permission service with 30+ granular permissions
- Feature flag system (10 flags)
- All SignalR hubs role-gated with `[Authorize(Roles = "...")]`

## Data Protection

- Passwords stored as BCrypt hashes (never plaintext)
- JWT secret must be ≥32 characters — startup fails if default detected
- Screenshots HMAC-signed for integrity verification
- Device hardware ID hashed with SHA256

## Hardening (Phase 15.5)

- JWT default secret detection — startup throws `InvalidOperationException` if unset
- Remote API disabled by default (opt-in)
- Demo users only created in Development environment
- CORS restricts origins when `Cors:AllowedOrigins` is configured
- HTTPS redirection enabled via `UseHttpsRedirection()`
- No hardcoded API keys in production defaults
- Thread-safe singleton services (all shared state protected with locks)
- Evidence store capped at 10,000 entries
- Auto-cleanup of stale stream sessions (5-minute timer)
- Connection tracking cleaned on SignalR disconnect

## Production Checklist

- [ ] JWT secret is a cryptographically random string (use `openssl rand -base64 32`)
- [ ] MySQL connection uses a dedicated user with limited privileges
- [ ] `ASPNETCORE_ENVIRONMENT=Production` (disables seed data)
- [ ] HTTPS configured (Kestrel cert or reverse proxy)
- [ ] CORS origins restricted to known frontend URLs
- [ ] Remote API disabled if not used
- [ ] Logs directory secured (contains no secrets)
- [ ] Screenshots and evidence directories secured

## Threat Model

| Threat | Mitigation |
|---|---|
| JWT token theft | Short expiration (15 min), refresh token rotation |
| Unauthorized API access | Role-based authorization on all endpoints |
| Detection engine tampering | Anti-tamper service, DLL integrity hashing |
| Debugger attachment | PEB/NtQueryInformationProcess detection |
| Database compromise | BCrypt hashing, parameterized queries (EF Core) |
| Replay attacks | Token lifetime validation, clock skew = zero |
| Cross-origin attacks | Configurable CORS, SignalR requires auth |
