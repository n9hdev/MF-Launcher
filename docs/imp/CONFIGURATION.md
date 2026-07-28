# Configuration

## Configuration Sources (priority order)

1. Environment variables (highest)
2. Command-line arguments
3. `appsettings.json`
4. `appsettings.{Environment}.json`

## Reference

### Jwt

| Key | Default | Description |
|---|---|---|
| `Jwt:Secret` | _(none — startup fails if empty/default)_ | HMAC-SHA256 signing key (≥32 characters) |
| `Jwt:Issuer` | `AntiCheatV6` | Token issuer claim |
| `Jwt:Audience` | `AntiCheatV6.Client` | Token audience claim |
| `Jwt:AccessTokenExpirationMinutes` | `15` | Access token lifetime |
| `Jwt:RefreshTokenExpirationDays` | `7` | Refresh token lifetime |

Environment variable override: `Jwt__Secret`, `Jwt__Issuer`, etc.

### ConnectionStrings

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | `Server=localhost;Database=mafia_security;User=root;Password=;` | MySQL connection string |

Environment variable override: `ConnectionStrings__DefaultConnection`

### RemoteApi

| Key | Default | Description |
|---|---|---|
| `RemoteApi:Enabled` | `false` | Enable remote API integration |
| `RemoteApi:BaseUrl` | _(empty)_ | Remote API server base URL |
| `RemoteApi:ApiKey` | _(empty)_ | Remote API authentication key |
| `RemoteApi:HeartbeatIntervalSeconds` | `5` | Heartbeat interval |
| `RemoteApi:RequestTimeoutSeconds` | `10` | HTTP request timeout |

### Cors

| Key | Default | Description |
|---|---|---|
| `Cors:AllowedOrigins:0` | _(empty — falls back to AllowAnyOrigin)_ | Allowed origin (repeat index N for multiple) |

Example:
```json
{
  "Cors": {
    "AllowedOrigins": ["http://localhost:5173", "https://client.example.com"]
  }
}
```

### Serilog

| Key | Default | Description |
|---|---|---|
| `Serilog:MinimumLevel:Default` | `Information` | Minimum log level |
| `Serilog:MinimumLevel:Override:Microsoft` | `Warning` | Microsoft namespace log level |
| `Serilog:MinimumLevel:Override:System` | `Warning` | System namespace log level |

### FeatureFlags

| Key | Default | Description |
|---|---|---|
| `FeatureFlags:detection.memory_scanner` | `true` | Enable memory scanner detector |
| `FeatureFlags:detection.process_analyzer` | `true` | Enable process analyzer detector |
| `FeatureFlags:detection.injection_detector` | `true` | Enable injection detector |
| `FeatureFlags:detection.kernel_scanner` | `true` | Enable kernel scanner detector |
| `FeatureFlags:detection.yara_scanner` | `true` | Enable YARA scanner detector |
| `FeatureFlags:detection.network_monitor` | `true` | Enable network monitor |
| `FeatureFlags:experimental.real_time_protection` | `false` | Experimental real-time protection |
| `FeatureFlags:experimental.ai_detection` | `false` | Experimental AI detection |
| `FeatureFlags:experimental.behavior_analysis` | `false` | Experimental behavior analysis |
| `FeatureFlags:maintenance_mode` | `false` | Enable maintenance mode |

## Environment Variable Format

Use double underscore `__` as separator:

```
Jwt__Secret=my-super-secure-key-here-at-least-32-chars-long
ConnectionStrings__DefaultConnection=Server=dbhost;Database=mafia_security;User=app;Password=secret
RemoteApi__Enabled=true
RemoteApi__BaseUrl=http://10.147.20.39:9000
Cors__AllowedOrigins__0=http://localhost:5173
```

## Production Checklist

Before deploying to production:

- [ ] `Jwt:Secret` is a cryptographically random string ≥32 characters — NOT the default placeholder
- [ ] `ConnectionStrings:DefaultConnection` uses a dedicated MySQL user with limited privileges
- [ ] `ASPNETCORE_ENVIRONMENT` is set to `Production`
- [ ] `RemoteApi:Enabled` is `true` only if the remote API server is available
- [ ] `RemoteApi:ApiKey` is configured if RemoteApi is enabled
- [ ] `Cors:AllowedOrigins` lists the frontend origin(s)
- [ ] HTTPS certificate is configured (via Kestrel or reverse proxy)
- [ ] Logs directory exists and is writable
