# ARCHITECTURE — Mafia City Anti-Cheat V6

## Process Model

```
┌──────────────────────────────────────────────────────────────────────────┐
│                        ELECTRON MAIN PROCESS                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │   IPC    │  │   Tray   │  │ Updater  │  │  Window  │  │  Menu    │ │
│  │  Bridge  │  │  Manager │  │  Service │  │  Manager │  │  (App)   │ │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
                           │ preload.ts (contextBridge)
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      REACT RENDERER PROCESS                             │
│                                                                         │
│  ┌──────────────────────────────────────────────────────────────────┐  │
│  │                       ThemeProvider                               │  │
│  │  ┌────────────────────────────────────────────────────────────┐  │  │
│  │  │                   Layout (flex)                             │  │  │
│  │  │  ┌────────────────┐  ┌────────────────────────────────┐    │  │  │
│  │  │  │ AnimatedSidebar │  │         Main Content           │    │  │  │
│  │  │  │  - Dynamic nav  │  │  ┌──────────────────────────┐ │    │  │  │
│  │  │  │  per role       │  │  │    FloatingTopBar         │ │    │  │  │
│  │  │  │  - Expand/coll  │  │  │  - Breadcrumbs           │ │    │  │  │
│  │  │  │  - Sectioned    │  │  │  - Search (Ctrl+F)       │ │    │  │  │
│  │  │  └────────────────┘  │  │  - Commands (Ctrl+K)      │ │    │  │  │
│  │  │                      │  │  - Notifications          │ │    │  │  │
│  │  │                      │  │  - User menu              │ │    │  │  │
│  │  │                      │  │  └──────────────────────────┘ │    │  │  │
│  │  │                      │  │  ┌──────────────────────────┐ │    │  │  │
│  │  │                      │  │  │    <AnimatePresence>     │ │    │  │  │
│  │  │                      │  │  │  Page content (Routes)   │ │    │  │  │
│  │  │                      │  │  └──────────────────────────┘ │    │  │  │
│  │  │                      │  └────────────────────────────────┘    │  │  │
│  │  │                      │  ┌────────────────────────────────┐    │  │  │
│  │  │                      │  │       InfoDrawer (right)        │    │  │  │
│  │  │                      │  └────────────────────────────────┘    │  │  │
│  │  └────────────────────────────────────────────────────────────┘  │  │
│  │                                                                   │  │
│  │  Global overlays: CommandPalette, GlobalSearch, ContextMenu,      │  │
│  │  ToastSystem                                                      │  │
│  └──────────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘
                           │ IPC / Named Pipes
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      .NET BACKEND PROCESS                               │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐ │
│  │   API    │  │  SignalR │  │   Auth   │  │Detection │  │ Windows  │ │
│  │  Server  │  │   Hub    │  │  Service │  │  Engine  │  │ Service  │ │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘  └──────────┘ │
└──────────────────────────────────────────────────────────────────────────┘
                           │ HTTPS / SignalR
                           ▼
┌──────────────────────────────────────────────────────────────────────────┐
│                    REMOTE API SERVER (Existing)                          │
│                    http://10.147.20.39:9000/v2/priv8/*                   │
└──────────────────────────────────────────────────────────────────────────┘
```

## State Architecture

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  authStore   │  │    uiStore   │  │ detectionStore│  │ sessionStore │
│  - user      │  │  - sidebar   │  │  - events     │  │  - gameState │
│  - tokens    │  │  - modals    │  │  - status     │  │  - duration  │
│  - role      │  │  - toasts    │  │  - health     │  │              │
└──────────────┘  └──────────────┘  └──────────────┘  └──────────────┘
┌──────────────┐  ┌──────────────┐
│ notification │  │  settings   │
│    Store     │  │    Store    │
│  - list      │  │  - prefs    │
│  - unread    │  │  - theme    │
└──────────────┘  └──────────────┘
```

## Component Architecture

### Layout Components
- **AnimatedSidebar** — Sectioned navigation filtered by user role; expandable/collapsible with spring animation; active state indicator with layoutId
- **FloatingTopBar** — Breadcrumbs from URL path; search button (Ctrl+F); command palette button (Ctrl+K); theme toggle; notification bell with badge; user menu with dropdown
- **InfoDrawer** — Right-side slide panel for contextual details
- **CommandPalette** — Modal overlay with keyboard navigation; filters commands by query; role-aware items
- **GlobalSearch** — Full-text search overlay across players, pages, and settings
- **ContextMenu** — Right-click menu with position anchoring
- **ToastSystem** — Bottom-right stacked toasts with auto-dismiss

### UI Components (15 total)
All share: glassmorphism, spring animations, hover states, consistent color tokens.

1. **GlassCard** — Base card with blur, border, optional glow, hover lift
2. **AnimatedButton** — 6 variants (primary/secondary/ghost/danger/success/gradient), 3 sizes, loading state
3. **StatusCard** — Module status display with dot indicator
4. **MetricCard** — KPI display with trend arrows, loading skeleton
5. **ThreatCard** — Security alert with severity color, confidence bar, actions
6. **UserCard** — Player profile card with avatar, status dot, stats
7. **DetectorCard** — Module card with toggle, detections count, accuracy
8. **Timeline** — Vertical event timeline with icons and connector lines
9. **ActivityFeed** — Compact activity list with type icons
10. **NotificationCenter** — Full notification list with mark-read
11. **DataTable** — Sortable, searchable, paginated table
12. **TrustScore** — Animated ring gauge with color thresholds
13. **RiskGauge** — Segmented bar gauge for risk assessment
14. **AnimatedModal** — Centered modal with spring animation and backdrop
15. **SearchBar** — Input with icon, clear button, controlled/uncontrolled

## Page Inventory (23 pages)

| Route | Page | Role |
|-------|------|------|
| /dashboard | PlayerDashboard | All |
| /player/protection | ProtectionPage | Player |
| /player/launch | LaunchPage | Player |
| /player/history | HistoryPage | Player |
| /player/reports | PlayerReportsPage | Player |
| /moderator/reports | ReportsQueuePage | Mod/Admin/SA |
| /moderator/players | PlayerSearchPage | Mod/Admin/SA |
| /moderator/alerts | AlertsPage | Mod/Admin/SA |
| /moderator/chat | ModChatPage | Mod/Admin/SA |
| /admin/bans | BanCenterPage | Admin/SA |
| /admin/analytics | AnalyticsPage | Admin/SA |
| /admin/appeals | AppealsPage | Admin/SA |
| /admin/whitelist | WhitelistPage | Admin/SA |
| /superadmin/command | CommandCenterPage | SA |
| /superadmin/telemetry | TelemetryPage | SA |
| /superadmin/detection | DetectionCenterPage | SA |
| /superadmin/rules | RulesPage | SA |
| /superadmin/infrastructure | InfrastructurePage | SA |
| /superadmin/audit | AuditLogPage | SA |
| /settings | SettingsPage | All |

## Theme System

- **Dark** (default): Deep navy/slate surfaces, glass effects, primary indigo accent
- **High Contrast**: Increased opacity on glass, brighter text, stronger borders
- CSS custom properties for all colors, radii, shadows, fonts
- ThemeProvider context for React-side toggling
