# Deployment

## Production Architecture

```
[Electron Client] → [API Server] → [MySQL Database]
                        ↕
                 [Remote API Server] (optional)
                        ↕
                 [Windows Service] (optional)
```

## API Server Deployment

### Publish

```bash
cd src/backend/AntiCheat.Api

# Release build
dotnet publish -c Release -o publish

# Deploy publish/ contents to target server
```

### Configure

Set via environment variables (preferred) or `appsettings.json`:

| Variable | Required | Description |
|---|---|---|
| `Jwt__Secret` | **Yes** | JWT signing key (≥32 chars, cryptographically random) |
| `ConnectionStrings__DefaultConnection` | **Yes** | MySQL connection string |
| `ASPNETCORE_URLS` | No | Listen address (default: `http://localhost:5000`) |
| `ASPNETCORE_ENVIRONMENT` | No | Set to `Production` for release mode |
| `RemoteApi__Enabled` | No | Set `true` to enable remote API integration |
| `RemoteApi__BaseUrl` | If enabled | Remote API server URL |
| `RemoteApi__ApiKey` | If enabled | Remote API authentication key |
| `Cors__AllowedOrigins__0` | No | Allowed CORS origin (repeat index for multiple) |

### Run as Windows Service

```bash
sc create MafiaCityAntiCheatApi binPath="C:\path\to\publish\AntiCheat.Api.exe"
sc start MafiaCityAntiCheatApi
```

Or run directly:
```bash
dotnet AntiCheat.Api.dll
```

### Run as Console

```bash
dotnet AntiCheat.Api.dll --urls "http://0.0.0.0:5000"
```

## Windows Service Deployment (Headless)

```bash
cd src/backend/AntiCheat.Service
dotnet publish -c Release -o publish
sc create MafiaCityAntiCheatV6 binPath="C:\path\to\publish\AntiCheat.Service.exe"
sc start MafiaCityAntiCheatV6
```

The Windows Service runs the detection engine in the background without the API, suitable for dedicated anti-cheat servers.

## Frontend Deployment

### Production Build

```bash
cd src/frontend
npm ci
npm test
npm run build
# Output: dist/renderer/ (Vite build)
# Output: release/ (Electron package, if electron-builder configured)
```

### Electron Building

```bash
npm run build
# Produces:
#   release/win-unpacked/   (extracted)
#   release/*.exe          (installer, if configured)
```

### Configuration

Set `VITE_API_BASE_URL` in `.env` to point to the production API server.

## Database Migration

Current deployment uses `db.Database.EnsureCreated()` which creates tables on first run.

For schema changes, either:
1. Use EF Core migrations: `dotnet ef migrations add` + `dotnet ef database update`
2. Manual SQL scripts

> **Note**: EnsureCreated() does not support incremental migrations. For production upgrades, switch to EF Core migrations or manual SQL.

## Backup Procedures

### Database
```bash
mysqldump -u root -p mafia_security > backup_$(date +%Y%m%d).sql
```

### Configuration
Backup `appsettings.json` and any environment variable configurations.

### Screenshots & Evidence
Backup the `screenshots/` and `logs/evidence/` directories.

## Update Process

1. Stop the service: `sc stop MafiaCityAntiCheatApi`
2. Backup database and configuration
3. Replace binaries
4. Update configuration if schema changed
5. Start the service: `sc start MafiaCityAntiCheatApi`
6. Verify health: check logs and API response
7. Monitor for errors in the first 5 minutes

## Rollback

See [PRODUCTION_CHECKLIST.md](../PRODUCTION_CHECKLIST.md) for the complete rollback plan.
