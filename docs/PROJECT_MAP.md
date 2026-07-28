# PROJECT MAP — Mafia City Anti-Cheat V6

## Repository Structure

```
mafia-city-anticheat-v6/
├── .github/workflows/ci.yml
├── docs/
│   ├── LEGACY_ANALYSIS.md
│   ├── PROJECT_MAP.md
│   ├── ARCHITECTURE.md
│   ├── ROADMAP.md
│   ├── CODING_STANDARDS.md
│   ├── API_CHANGES.md
│   └── CHANGELOG.md
├── scripts/
│   ├── build.ps1
│   └── dev.ps1
├── src/
│   ├── frontend/                          # Electron + React application
│   │   ├── index.html
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   ├── vite.config.ts
│   │   ├── tailwind.config.ts
│   │   ├── postcss.config.js
│   │   └── src/
│   │       ├── main/                      # Electron main process
│   │       │   ├── main.ts
│   │       │   ├── ipc/handlers.ts
│   │       │   ├── tray/manager.ts
│   │       │   ├── updater/service.ts
│   │       │   └── window/manager.ts
│   │       ├── preload/preload.ts
│   │       └── renderer/                  # React renderer
│   │           ├── App.tsx                # Root with auth-aware routing
│   │           ├── main.tsx               # Entry point
│   │           ├── theme/                 # Design system
│   │           │   ├── ThemeProvider.tsx
│   │           │   └── tokens.ts
│   │           ├── stores/                # State (6 stores)
│   │           │   ├── authStore.ts
│   │           │   ├── uiStore.ts
│   │           │   ├── detectionStore.ts
│   │           │   ├── notificationStore.ts
│   │           │   ├── sessionStore.ts
│   │           │   └── settingsStore.ts
│   │           ├── types/
│   │           │   ├── global.d.ts        # All shared types
│   │           │   └── electron.d.ts
│   │           ├── components/
│   │           │   ├── layout/
│   │           │   │   ├── AnimatedSidebar.tsx     # Expandable, role-filtered
│   │           │   │   ├── FloatingTopBar.tsx       # Breadcrumbs, search, notif, user
│   │           │   │   ├── InfoDrawer.tsx           # Right-side slide panel
│   │           │   │   ├── CommandPalette.tsx       # Ctrl+K modal
│   │           │   │   ├── GlobalSearch.tsx         # Ctrl+F modal
│   │           │   │   ├── ContextMenu.tsx          # Right-click menu
│   │           │   │   └── ToastSystem.tsx          # Bottom-right toast stack
│   │           │   └── ui/
│   │           │       ├── GlassCard.tsx
│   │           │       ├── AnimatedButton.tsx
│   │           │       ├── StatusCard.tsx
│   │           │       ├── MetricCard.tsx
│   │           │       ├── ThreatCard.tsx
│   │           │       ├── UserCard.tsx
│   │           │       ├── DetectorCard.tsx
│   │           │       ├── Timeline.tsx
│   │           │       ├── ActivityFeed.tsx
│   │           │       ├── NotificationCenter.tsx
│   │           │       ├── DataTable.tsx
│   │           │       ├── TrustScore.tsx
│   │           │       ├── RiskGauge.tsx
│   │           │       ├── AnimatedModal.tsx
│   │           │       ├── SearchBar.tsx
│   │           │       └── index.ts
│   │           ├── styles/globals.css     # Theme CSS variables, glass, utilities
│   │           └── pages/
│   │               ├── auth/
│   │               │   └── LoginPage.tsx
│   │               ├── player/
│   │               │   ├── PlayerDashboard.tsx
│   │               │   ├── ProtectionPage.tsx
│   │               │   ├── LaunchPage.tsx
│   │               │   ├── HistoryPage.tsx
│   │               │   └── PlayerReportsPage.tsx
│   │               ├── moderator/
│   │               │   ├── ModeratorDashboard.tsx
│   │               │   ├── ReportsQueuePage.tsx
│   │               │   ├── PlayerSearchPage.tsx
│   │               │   ├── AlertsPage.tsx
│   │               │   └── ModChatPage.tsx
│   │               ├── admin/
│   │               │   ├── AdminDashboard.tsx
│   │               │   ├── BanCenterPage.tsx
│   │               │   ├── AnalyticsPage.tsx
│   │               │   ├── AppealsPage.tsx
│   │               │   └── WhitelistPage.tsx
│   │               ├── superadmin/
│   │               │   ├── CommandCenterPage.tsx
│   │               │   ├── TelemetryPage.tsx
│   │               │   ├── DetectionCenterPage.tsx
│   │               │   ├── RulesPage.tsx
│   │               │   ├── InfrastructurePage.tsx
│   │               │   └── AuditLogPage.tsx
│   │               └── shared/
│   │                   └── SettingsPage.tsx
│   └── backend/                            # .NET 8 solution
│       ├── AntiCheat.Api/                  # Web API + SignalR hub
│       ├── AntiCheat.Service/              # Windows background service
│       ├── AntiCheat.Core/                 # Interfaces + services
│       ├── AntiCheat.Detection/            # Detection engine
│       ├── AntiCheat.Launcher/             # Game launcher
│       └── AntiCheat.Shared/               # DTOs
├── tests/
│   ├── frontend/
│   └── backend/
├── MafiaCityAntiCheat.sln
└── .gitignore
```

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Desktop Shell | Electron 33 |
| UI Framework | React 18 + TypeScript |
| Styling | TailwindCSS 3 + CSS custom properties |
| Animation | Framer Motion 11 |
| State Management | Zustand 4 (6 stores) |
| IPC | Electron IPC + preload bridge |
| Backend | .NET 8 (C# 12) |
| Real-time | SignalR |
| Database | SQLite (local) + existing API (remote) |
| Logging | Serilog |
| Detection | Managed Windows APIs + YARA |
| CI/CD | GitHub Actions |
| Packaging | electron-builder + MSI |
