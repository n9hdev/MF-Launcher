# Mafia City Anti-Cheat V6

A real-time anti-cheat system built for GTA San Andreas Multiplayer (MTA:SA). Protects servers by detecting cheats, memory tampering, and suspicious behavior — with a full management dashboard for staff.

---

## What It Does

**For Players**
- Runs silently in the background as a Windows service
- Scans for cheat engines, injected DLLs, debuggers, and memory modifications
- Verifies game file integrity before launch
- Provides a dashboard to view protection status, submit reports, and chat with staff
- Auto-updates to the latest version on startup

**For Staff (Moderators / Admins)**
- Web-based dashboard to manage the entire anti-cheat system
- Review and investigate player reports with built-in chat
- Flag suspicious players for deeper investigation
- Capture live screenshots and stream from a player's PC remotely
- Ban players with appeal management
- View analytics, detection events, and real-time alerts

---

## Key Features

| Feature | Description |
|---------|-------------|
| **Cheat Detection** | Memory scanning, process monitoring, injection detection, YARA rules, network monitoring |
| **HWID Tracking** | Hardware fingerprinting to prevent ban evasion and HWID spoofing |
| **Live Screen Capture** | Remotely capture screenshots or live stream a player's screen for investigation |
| **Ticket & Chat System** | Players submit reports; staff responds with a built-in per-ticket chat |
| **Flagged Players** | Separate investigation queue for high-priority suspicious players |
| **Auto-Updates** | Self-updating client with signed manifests and GitHub fallback download |
| **Ban System** | Full ban lifecycle with appeals, evidence, and appeal messages |
| **Role-Based Access** | Player, Moderator, Admin, and Superadmin roles with granular permissions |
| **Detection Modules** | Configurable detection engines that can be toggled per-module |
| **Audit Logging** | Full audit trail of all staff actions |

---

## System Requirements

- **OS:** Windows 10/11 (64-bit)
- **Server:** Windows or Linux with .NET 8 runtime
- **Database:** MySQL 5.7+ or MariaDB 10.3+

---

## Installation

1. Download the latest installer from [Releases](https://github.com/n9hdev/MF-Launcher/releases)
2. Run `MafiaCityAntiCheat-Setup.exe`
3. Follow the installation wizard
4. The client will start automatically and connect to the server

The installer is Authenticode-signed and all updates are verified with SHA-256 hashes.

---

## How It Works

```
Player's PC                          Server
┌────────────────────┐        ┌────────────────────┐
│  Anti-Cheat        │◄──────►│  API Server        │
│  Service           │  HTTP  │  (Dashboard + DB)  │
│                    │        │                    │
│  - Cheat scanning  │        │  - Ban management  │
│  - Heartbeat       │        │  - Report system   │
│  - Screen capture  │        │  - Analytics       │
│  - Pre-launch scan │        │  - Live monitoring │
└────────────────────┘        └────────────────────┘
```

Every 5 seconds, the player's service sends a heartbeat to the server. The server responds with any pending commands (screenshot requests, stream sessions, ban alerts). All detection events are reported in real-time.

---

## Staff Dashboard Roles

| Role | Access |
|------|--------|
| **Player** | Dashboard, reports, chat, ban appeals |
| **Moderator** | Reports queue, flagged players, chat moderation, screen capture |
| **Admin** | Bans, analytics, live player view, whitelist |
| **Superadmin** | Full system control: detection config, rules, infrastructure, audit logs |

---

## Release History

| Version | Date | Changes |
|---------|------|---------|
| **v6.3.2** | 2026-07-27 | Bug fixes: chat toggle persistence, screenshot/stream connectivity |
| **v6.3.1** | 2026-07-27 | Ticket & chat system, player flagging, screenshot/stream capture |
| **v6.3.0** | 2026-07-26 | Auto-update system, signed installers, GitHub fallback download |
| **v6.0.0** | 2025-12-24 | Initial V6 release |

---

## Support

If you encounter issues, open an issue on the [GitHub Issues](https://github.com/n9hdev/MF-Launcher/issues) page.

---

**Private Software — Mafia City / MF CITY, Inc.**
