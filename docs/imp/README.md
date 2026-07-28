# Mafia City Anti-Cheat V6

Real-time anti-cheat system for Mafia City (MTA:SA) with detection engine, live monitoring, role-based administration, and remote API integration.

## Architecture

- **Backend**: .NET 8 ASP.NET Core API + Windows Service
- **Frontend**: React 18 + TypeScript + Vite + Electron
- **Database**: MySQL 8.0 (Pomelo EF Core)
- **Real-time**: SignalR (dual hubs: events + screen streaming)
- **Auth**: JWT (HS256) with BCrypt password hashing

## Quick Start

```
# Backend
cd src/backend/AntiCheat.Api
dotnet run

# Frontend (dev)
cd src/frontend
npm run dev

# Frontend (desktop)
npm run dev:electron
```

See [INSTALL.md](INSTALL.md) for full setup.
See [DEPLOYMENT.md](DEPLOYMENT.md) for production deployment.
See [CONFIGURATION.md](CONFIGURATION.md) for configuration reference.
