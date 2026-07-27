# Mafia City Anti-Cheat V6

A full-stack anti-cheat system for GTA San Andreas Multiplayer (MTA:SA). Monitors player clients in real-time, detects cheats and memory tampering, and provides a web-based dashboard for moderators and admins to manage bans, investigate reports, and view live player sessions.

---

## Architecture

```
┌──────────────────────┐     HTTP / SignalR     ┌──────────────────────┐
│   Player Service     │ ◄───────────────────► │      API Server      │
│  (Windows Service)   │                        │   (.NET 8 + MySQL)   │
│  Runs on each PC     │                        │  Hosted on server    │
└──────────────────────┘                        └──────────┬───────────┘
                                                           │
                                              ┌────────────┼────────────┐
                                              │            │            │
                                     ┌────────▼──┐  ┌──────▼─────┐  ┌──▼──────────┐
                                     │  Desktop  │  │    Web     │  │  Database   │
                                     │  Client   │  │  Dashboard │  │  (MySQL)    │
                                     │ (Electron)│  │ (React)    │  │             │
                                     └───────────┘  └────────────┘  └─────────────┘
```

### Components

| Component | Location | Description |
|-----------|----------|-------------|
| **API Server** | `src/backend/AntiCheat.Api/` | ASP.NET Core 8 Web API. Handles auth, bans, detection events, reports, real-time SignalR hubs, auto-updates, and screen streaming relay. |
| **Player Service** | `src/backend/AntiCheat.Service/` | .NET 8 Windows Service running on each player's PC. Performs heartbeat with API, scans for cheats (memory, process, injection), captures screenshots/streams, and reports detections. |
| **Desktop Client** | `src/frontend/` | Electron + React + Vite + TypeScript app. Serves as the player dashboard and the moderator/admin web panel. Uses HashRouter for Electron compatibility. |
| **Shared Libraries** | `src/backend/AntiCheat.Core/` | Shared EF Core entities, services, interfaces. |
| | `src/backend/AntiCheat.Shared/` | DTOs, request/response models shared across all projects. |
| | `src/backend/AntiCheat.Detection/` | Cheat detection engine (memory scanning, process analysis, injection detection, YARA rules). |
| | `src/backend/AntiCheat.Launcher/` | Game launcher utilities. |

---

## Tech Stack

**Backend**
- .NET 8 / C#
- Entity Framework Core + Pomelo MySQL
- SignalR (real-time communication)
- Serilog (structured logging)
- JWT authentication with role-based access (player / moderator / admin / superadmin)
- MySQL / MariaDB database

**Frontend (Desktop Client)**
- Electron 43
- React 18 + TypeScript
- Vite 8 (bundler)
- Tailwind CSS
- Framer Motion (animations)
- Zustand (state management)
- Lucide React (icons)
- React Router 6

**Installer**
- Inno Setup 6
- Authenticode code signing (self-signed cert)
- SHA-256 manifest verification
- ECDSA-signed update manifests

---

## Features

### Detection Engine
- Memory scanner (cheat engine, injected DLLs)
- Process analyzer (suspicious processes, debuggers)
- Injection detector (code injection, hooking)
- Kernel-level scanner
- YARA rule matching
- Network monitor
- Game hash verification (MTA:SA binary integrity)
- HWID spoofing detection

### Player Experience
- Real-time protection status dashboard
- Pre-launch game integrity scan
- Game path and MTA:SA serial detection
- Player ticket/report submission with chat
- Ban appeal system
- Screenshot & stream capture (when moderated by staff)

### Moderator Tools
- Reports queue (non-flagged tickets)
- Flagged players queue (separate investigation list)
- Per-ticket chat system (moderator-controlled toggle)
- Player search and detail view
- Real-time alerts
- Status management (pending / investigating / resolved / dismissed)
- Screen capture and live streaming from player PCs

### Admin & Superadmin
- Ban center with appeal management
- Analytics and threat intelligence
- Live player view
- Detection module management and rule editor
- Infrastructure health monitoring
- Telemetry and resource monitoring
- Audit log viewer
- Whitelist management
- Command center with system-wide controls

### Auto-Update System
- Client checks API for new versions on startup
- Manifest signed with ECDSA (SHA-256) -- tamper-proof
- Primary download from API server, GitHub Releases as fallback
- Authenticode-signed installer verification
- Critical update modal (forced update for security patches)

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Node.js 20+](https://nodejs.org/) (for frontend build)
- [MySQL 5.7+](https://dev.mysql.com/downloads/mysql/) or MariaDB 10.3+
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (for installer builds, optional)
- Windows 10/11 x64 (for service and client builds)

---

## Getting Started

### 1. Database Setup

Create a MySQL database:

```sql
CREATE DATABASE mafia_security CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci;
```

The API will auto-create tables and columns on startup via `Program.cs` migrations.

Update the connection string in `src/backend/AntiCheat.Api/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=mafia_security;User=root;Password=yourpassword;"
  }
}
```

### 2. Build & Run the API

```bash
dotnet restore MafiaCityAntiCheat.sln
dotnet build MafiaCityAntiCheat.sln -c Release
dotnet run --project src/backend/AntiCheat.Api -c Release
```

The API starts on `http://0.0.0.0:5000`.

To install as a Windows Service:

```bash
dotnet publish src/backend/AntiCheat.Api -c Release -o C:\AntiCheat\Api
sc create AntiCheatApi binPath="C:\AntiCheat\Api\AntiCheat.Api.exe" start=auto
sc start AntiCheatApi
```

### 3. Build & Run the Player Service

```bash
dotnet publish src/backend/AntiCheat.Service -c Release -r win-x64 --self-contained -o publish
```

The service connects to the API at the address configured in `ServiceDeployConfig.ApiBaseUrl` (default: `http://25.20.173.193:5000`).

Install as a Windows Service on the player's PC:

```bash
sc create AntiCheatService binPath="C:\path\to\AntiCheat.Service.exe" start=auto
sc start AntiCheatService
```

### 4. Build the Desktop Client

```bash
cd src/frontend
npm install
npm run build
```

For development:

```bash
npm run dev:electron
```

This starts Vite dev server + Electron in development mode.

### 5. Build the Installer (Optional)

Requires Inno Setup 6 installed at `C:\Program Files (x86)\Inno Setup 6\ISCC.exe`.

```bash
cd src/frontend
npm run build  # Produces release/Mafia City Anti-Cheat V6 Setup.exe
```

---

## Deployment

### API Server

A deploy script is provided at `C:\AntiCheat\deploy-api.bat` (run as Administrator):

```bat
net stop AntiCheatApi
xcopy /E /Y /I "C:\AntiCheat\ApiPublish\*" "C:\AntiCheat\Api\"
net start AntiCheatApi
```

### Update System

1. Build the new installer
2. Sign it with Authenticode: `signtool sign /f keys/code-signing.pfx /p "MafiaCity2026!" /tr http://timestamp.digicert.com /td sha256 /fd sha256 installer.exe`
3. Run the manifest signing script:

```bash
node scripts/sign-update-manifest.js <version> <sha256> <size> [isCritical] [changelog]
```

This updates `appsettings.json` with the new update info and generates a signed manifest.

4. Copy the installer to the API's `updates/` directory
5. Restart the API service

Clients will automatically detect the new version on next startup.

---

## Configuration

### Feature Flags (`appsettings.json`)

| Flag | Default | Description |
|------|---------|-------------|
| `detection.memory_scanner` | `true` | Memory scan for cheat signatures |
| `detection.process_analyzer` | `true` | Process monitoring |
| `detection.injection_detector` | `true` | DLL injection detection |
| `detection.kernel_scanner` | `true` | Kernel-level scanning |
| `detection.yara_scanner` | `true` | YARA rule matching |
| `detection.network_monitor` | `true` | Network activity monitoring |
| `experimental.real_time_protection` | `false` | Real-time file protection |
| `experimental.ai_detection` | `false` | AI-based detection |
| `maintenance_mode` | `false` | Disable all detection |

### Roles

| Role | Access Level |
|------|-------------|
| `player` | Dashboard, reports, chat, ban appeals |
| `moderator` | Reports queue, flagged players, chat moderation, screen capture |
| `admin` | Bans, analytics, live player view, whitelist |
| `superadmin` | Full access: detection config, rules, infrastructure, audit logs |

---

## Project Structure

```
MafiaCityAntiCheat.sln
├── src/
│   ├── backend/
│   │   ├── AntiCheat.Api/          # Web API + SignalR hubs
│   │   │   ├── Controllers/        # REST endpoints
│   │   │   ├── Hubs/               # SignalR real-time hubs
│   │   │   └── Services/           # Background services
│   │   ├── AntiCheat.Core/         # Shared business logic
│   │   │   ├── Configuration/      # Options classes
│   │   │   ├── Data/               # DbContext, entities
│   │   │   ├── Interfaces/         # Service interfaces
│   │   │   └── Services/           # Service implementations
│   │   ├── AntiCheat.Detection/    # Cheat detection engine
│   │   ├── AntiCheat.Launcher/     # Game launcher
│   │   ├── AntiCheat.Service/      # Player Windows Service
│   │   └── AntiCheat.Shared/       # DTOs, models
│   └── frontend/
│       └── src/
│           ├── main/               # Electron main process
│           │   ├── updater/        # Auto-update (download, verify, sign)
│           │   └── ipc/            # IPC handlers
│           ├── preload/            # Electron preload script
│           └── renderer/           # React app
│               ├── components/     # Reusable UI components
│               ├── pages/          # Route pages (player, moderator, admin, superadmin)
│               ├── services/       # API service layer (auth, reports, moderator, etc.)
│               ├── stores/         # Zustand state stores
│               └── types/          # TypeScript type definitions
├── scripts/
│   ├── sign-update-manifest.js     # Signs and distributes update manifests
│   └── generate-update-keys.js     # Generates ECDSA key pair for manifests
├── keys/
│   ├── code-signing.pfx            # Authenticode code signing cert
│   ├── update-private.pem          # Manifest signing private key
│   └── update-public.pem           # Manifest verification public key
└── installer/
    └── setup.iss                   # Inno Setup script
```

---

## Release History

| Version | Date | Changes |
|---------|------|---------|
| **v6.3.2** | 2026-07-27 | Bug fixes: chat toggle persistence, screenshot/stream connectivity, robust MySQL migration, GitHub fallback download |
| **v6.3.1** | 2026-07-27 | Ticket & chat system, player flagging, screenshot/stream capture, Authenticode signing, GitHub fallback download |
| **v6.3.0** | 2026-07-26 | Auto-update system, detached installer, PowerShell deployment |
| **v6.0.0** | 2025-12-24 | Initial V6 release |

---

## License

Private - Mafia City / MF CITY, Inc.
