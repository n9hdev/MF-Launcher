# API Reference

Base URL: `http://localhost:5000` (configurable)

## Authentication

All endpoints except `/api/auth/*` require JWT bearer token.

Token format: `Authorization: Bearer <token>`

### POST /api/auth/login
Login with username and password.

Request:
```json
{
  "username": "string",
  "password": "string",
  "deviceId": "string (optional)",
  "ipAddress": "string (optional)"
}
```

Response: `LoginResponse` with access token, refresh token, and session.

### POST /api/auth/register
Register a new user (player role).

### POST /api/auth/refresh
Exchange a refresh token for a new access/refresh token pair.

### POST /api/auth/logout
Terminate current session and revoke all refresh tokens.

### GET /api/auth/me
Get current user profile.

## Hubs (SignalR)

### /hub/anticheat
Real-time detection events and status updates.

Methods:
- `JoinRoleGroup(role)` — Join a role-specific notification group
- `RequestScan()` — Trigger a full scan (results via `ScanResults` event)
- `RequestStatus()` — Get current protection status

Events:
- `StatusUpdate` — Protection status (sent on connect)
- `DetectionEvent` — New detection event
- `ScanResults` — Full scan results

### /hub/screenstream
[Authorize: moderator, admin, superadmin]

Live screen streaming from monitored clients.

## Controllers

### DetectionController (`/api/detection`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/detection/scan` | Any auth | Run full scan |
| GET | `/api/detection/status` | Any auth | Get protection status |
| GET | `/api/detection/detectors` | Any auth | List all detectors |
| POST | `/api/detection/detectors/{name}/enable` | Admin+ | Enable detector |
| POST | `/api/detection/detectors/{name}/disable` | Admin+ | Disable detector |
| POST | `/api/detection/assess` | Admin+ | Assess risk for events |
| GET | `/api/detection/confidence` | Admin+ | Get confidence history |
| GET | `/api/detection/evidence/{eventId}` | Moderator+ | Get evidence |
| GET | `/api/detection/rules` | SuperAdmin | List rules |
| POST | `/api/detection/rules` | SuperAdmin | Create/update rule |
| DELETE | `/api/detection/rules/{id}` | SuperAdmin | Delete rule |
| GET | `/api/detection/correlation` | SuperAdmin | Get correlation status |

### ScreenCaptureController (`/api/screen`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| POST | `/api/screen/capture` | Moderator+ | Request screenshot |
| GET | `/api/screen/history/{playerId}` | Moderator+ | Screenshot history |
| POST | `/api/screen/sign` | Moderator+ | Sign screenshot |
| POST | `/api/screen/streams/create` | Moderator+ | Create stream session |
| POST | `/api/screen/streams/{sessionId}/end` | Moderator+ | End stream |
| GET | `/api/screen/streams/active` | Moderator+ | List active streams |
| PUT | `/api/screen/streams/{sessionId}/fps` | Moderator+ | Update FPS |
| GET | `/api/screen/evidence/{detectionId}` | Moderator+ | Get linked evidence |

### PermissionController (`/api/permission`)

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| GET | `/api/permission/my` | Any auth | Get current user permissions |
| GET | `/api/permission/all` | Admin+ | Get all permissions |
| GET | `/api/permission/flags` | Any auth | Get all feature flags |
| POST | `/api/permission/check` | Any auth | Check specific permission |

### AuthController (`/api/auth`)

As described in Authentication section.

### Other Controllers

| Controller | Prefix | Description |
|---|---|---|
| `ActivityController` | `/api/activity` | Player activity timeline |
| `HistoryController` | `/api/history` | Detection history |
| `PlayerReportsController` | `/api/reports` | Player report submission |
| `GameController` | `/api/game` | Game launch/status |
| `ModeratorController` | `/api/moderator` | Moderator tools |
| `ModChatController` | `/api/modchat` | Moderator chat |
| `AdminController` | `/api/admin` | Admin tools |
| `SuperAdminController` | `/api/superadmin` | SuperAdmin tools |

## Remote API (External)

If `RemoteApi.Enabled=true`, the API calls these external endpoints:

| Method | Endpoint | Frequency | Description |
|---|---|---|---|
| POST | `/v2/priv8/heartbeat` | Every 5s | Send heartbeat with HWID + game status |
| POST | `/v2/priv8/globalverify` | On verify | Global verification request |
| POST | `/v2/priv8/antireport` | On detection ≥0.8 | Report detection event |
| GET | `/v2/priv8/anticheat/update-check` | On check | Check for updates |
| GET | `/v2/priv8/getprofile` | On profile request | Get player profile |

## Error Responses

Standard error format:
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid credentials"
}
```

HTTP status codes:
- `200` — Success
- `400` — Bad request
- `401` — Unauthorized
- `403` — Forbidden (insufficient role)
- `404` — Not found
- `500` — Internal server error
