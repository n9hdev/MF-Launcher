# Installation

## Prerequisites

- Windows 10/11 or Windows Server 2019+
- .NET 8.0 SDK + Runtime
- Node.js 18.x (v18.20.4 tested)
- MySQL 8.0+ server
- npm 9+

## Backend Setup

```bash
cd src/backend

# Restore NuGet packages
dotnet restore MafiaCityAntiCheat.sln

# Build all projects
dotnet build MafiaCityAntiCheat.sln

# Run tests
dotnet test AntiCheat.Tests/AntiCheat.Tests.csproj
```

### API Server

```bash
cd AntiCheat.Api
dotnet run
# Listens on http://localhost:5000 by default
```

### Windows Service

```bash
cd AntiCheat.Service
dotnet publish -c Release -o publish
sc create MafiaCityAntiCheatV6 binPath="$(pwd)\publish\AntiCheat.Service.exe"
sc start MafiaCityAntiCheatV6
```

## Frontend Setup

```bash
cd src/frontend

# Install dependencies
npm install

# Run tests
npm test

# Development server
npm run dev
# Opens at http://localhost:5173

# Electron development
npm run dev:electron
```

### Environment

Copy or configure `src/frontend/.env`:

```
VITE_API_BASE_URL=http://localhost:5000
VITE_API_TIMEOUT=10000
```

## MySQL Configuration

1. Ensure MySQL 8.0+ is running
2. Create database: `CREATE DATABASE mafia_security CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;`
3. Configure connection string in `appsettings.json` or via `ConnectionStrings__DefaultConnection` environment variable

Default connection string:
```
Server=localhost;Database=mafia_security;User=root;Password=;
```

> **Security**: Create a dedicated MySQL user with limited privileges for production.

## First Launch

1. Start the API: `dotnet run` (from `src/backend/AntiCheat.Api`)
2. Database tables are auto-created via `EnsureCreated()`
3. In **Development** mode only, seed users are created:
   - Player1 / player
   - Mod1 / mod
   - Admin1 / admin
   - Super1 / super
4. Start the frontend: `npm run dev` (from `src/frontend`)
5. Login with a seed account (development) or register via the auth endpoint
6. Background scanning starts automatically (30s adaptive interval)

## Troubleshooting

### Database connection fails
- Verify MySQL is running: `mysql -u root -p`
- Check connection string in `appsettings.json`
- Ensure database exists: `CREATE DATABASE mafia_security;`

### JWT authentication errors
- Verify `Jwt:Secret` is set (≥32 chars)
- Check token expiration (default 15 min)
- Ensure clock skew is configured (default zero)

### SignalR connection fails
- Check `VITE_API_BASE_URL` in frontend `.env`
- Verify CORS configuration in `appsettings.json`
- Ensure API server is reachable from client

### Frontend build fails
- `npm install` may have cached stale packages — run `npm ci`
- Node.js 18+ required
- If `jsdom` errors occur, frontend uses `happy-dom` (Node 18 compatible)
