# Screen Capture & Live Streaming Architecture

## 1. Problem Statement

The Windows Service runs in Session 0 (non-interactive). Screen capture APIs (GDI CopyFromScreen, DXGI Desktop Duplication, Windows.Graphics.Capture) all require an interactive desktop context. Session 0 isolation is enforced at the kernel level since Windows Vista — no amount of token manipulation, privilege escalation, or desktop switching can bypass it.

**The old anticheat (v4) solved this by being a desktop app** (WinForms), not a Windows Service. It ran in the user's session where `CopyFromScreen` worked natively.

**This design moves capture to the Electron app** (which already runs in the user's session) while keeping the Windows Service as the trusted orchestrator.

---

## 2. Component Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        Backend API Server                       │
│                  (25.20.173.193:5000 / MySQL)                   │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌───────────────────────┐ │
│  │ ScreenshotCtrl│  │StreamCtrl/Hub│  │ ServiceCtrl (JWT Auth)│ │
│  └──────┬───────┘  └──────┬───────┘  └───────────┬───────────┘ │
└─────────┼──────────────────┼──────────────────────┼─────────────┘
          │ HTTP/SignalR     │ WebSocket            │ HTTP
          │                  │                      │
┌─────────┼──────────────────┼──────────────────────┼─────────────┐
│         ▼                  ▼                      ▼             │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Windows Service (AntiCheat.Service)         │   │
│  │                  Runs as LocalSystem (Session 0)         │   │
│  │                                                          │   │
│  │  ┌─────────────┐  ┌──────────────┐  ┌───────────────┐   │   │
│  │  │AntiCheatWkr │  │ServiceScreen │  │NamedPipeServer│   │   │
│  │  │(heartbeat,  │  │  Capture     │  │  (IPC to UI)  │   │   │
│  │  │ detections) │  │  (orchestr.) │  │               │   │   │
│  │  └─────────────┘  └──────┬───────┘  └───────┬───────┘   │   │
│  └──────────────────────────┼───────────────────┼───────────┘   │
│                             │                   │                │
│                     Named Pipe IPC    Named Pipe IPC             │
│                             │                   │                │
│  ┌──────────────────────────┼───────────────────┼───────────┐   │
│  │              Electron App (User Session)                  │   │
│  │                                                          │   │
│  │  ┌──────────────┐  ┌───────────────┐  ┌──────────────┐  │   │
│  │  │  Renderer    │  │  main.ts      │  │ NativeCapture│  │   │
│  │  │  (React UI)  │←→│  (IPC Router) │←→│  Worker.exe  │  │   │
│  │  │              │  │               │  │ (optional)   │  │   │
│  │  └──────────────┘  └───────────────┘  └──────────────┘  │   │
│  └──────────────────────────────────────────────────────────┘   │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Runs In | Role |
|---|---|---|
| **Backend API** | Server | Stores screenshots/streams in MySQL, serves admin panel, relays commands |
| **Windows Service** | Session 0 (SYSTEM) | Orchestrator. Receives commands from API. Forwards to Electron via named pipe. Stores detections. |
| **Electron App** | User Session | Capture controller. Performs actual screen capture. Returns images to service. |
| **NativeCaptureWorker.exe** | User Session (spawned) | Optional. High-perf DXGI DD + NVENC for streaming. Spawned by Electron. |

---

## 3. IPC Protocol — Named Pipes

### Pipe Name
```
\\.\pipe\mf-anticheat-capture
```

### Protocol Format (Message Mode)

All messages use a simple TLV (Type-Length-Value) binary format:

```
[4 bytes: MessageLength (uint32 LE)]
[1 byte:  MessageType]
[Variable: Payload (JSON or binary)]
```

### Message Types

#### Service → Electron (Commands)

| Type ID | Name | Payload | Description |
|---|---|---|---|
| `0x01` | `CAPTURE_SCREENSHOT` | `{ requestId, quality, width?, height? }` | Request single screenshot |
| `0x02` | `START_STREAM` | `{ requestId, sessionId, targetFps, jpegQuality }` | Start live streaming |
| `0x03` | `STOP_STREAM` | `{ requestId }` | Stop live streaming |
| `0x04` | `HEARTBEAT` | `{}` | Liveness check (reply expected within 5s) |

#### Electron → Service (Responses & Events)

| Type ID | Name | Payload | Description |
|---|---|---|---|
| `0x81` | `SCREENSHOT_RESULT` | `{ requestId, success, imageData? (base64), width, height, error? }` | Screenshot response |
| `0x82` | `STREAM_FRAME` | `{ requestId, frameData (binary JPEG), timestamp }` | Single stream frame |
| `0x83` | `STREAM_STOPPED` | `{ requestId, reason }` | Stream ended notification |
| `0x84` | `HEARTBEAT_ACK` | `{}` | Heartbeat response |
| `0x85` | `STATUS` | `{ version, captureBackend, gpuAvailable, screenCount }` | Capabilities report |

### Binary Frame Protocol (Streaming)

For streaming, frames are sent as binary messages to avoid base64 overhead:

```
[4 bytes: MessageLength]
[1 byte:  0x82 (STREAM_FRAME)]
[4 bytes: requestId length]
[N bytes: requestId (UTF-8)]
[8 bytes: timestamp (int64 LE, Unix ms)]
[4 bytes: frameLength]
[N bytes: JPEG frame data]
```

### Electron-Side Named Pipe Client (Node.js)

```typescript
// main.ts — Named pipe connection to service
import * as net from 'net';

const PIPE_NAME = '\\\\.\\pipe\\mf-anticheat-capture';
let pipeClient: net.Socket | null = null;
let reconnectTimer: NodeJS.Timeout | null = null;

function connectToService(): void {
  pipeClient = net.createConnection(PIPE_NAME);
  
  pipeClient.on('connect', () => {
    console.log('[IPC] Connected to service');
    // Send capabilities
    sendMessage(0x85, { version: app.getVersion(), captureBackend: 'dxgi-dd', gpuAvailable: true, screenCount: screen.getAllDisplays().length });
  });
  
  pipeClient.on('data', (data: Buffer) => {
    handleMessage(data);
  });
  
  pipeClient.on('error', (err) => {
    console.error('[IPC] Pipe error:', err.message);
    scheduleReconnect();
  });
  
  pipeClient.on('close', () => {
    pipeClient = null;
    scheduleReconnect();
  });
}

function scheduleReconnect(): void {
  if (!reconnectTimer) {
    reconnectTimer = setTimeout(() => {
      reconnectTimer = null;
      connectToService();
    }, 5000);
  }
}

function sendMessage(type: number, payload: any): void {
  if (!pipeClient || !pipeClient.writable) return;
  const jsonBuf = Buffer.from(JSON.stringify(payload));
  const header = Buffer.alloc(5);
  header.writeUInt32LE(5 + jsonBuf.length, 0);
  header.writeUInt8(type, 4);
  pipeClient.write(Buffer.concat([header, jsonBuf]));
}
```

### Performance

| Metric | Value |
|---|---|
| Small message RTT | ~45µs |
| Large message throughput | ~95 MB/s |
| 30fps JPEG (100KB each) | 3 MB/s — 30x headroom |
| H.264 stream (5 Mbps) | 0.625 MB/s — 150x headroom |

Named pipes are well-suited for this use case.

---

## 4. Screenshot Workflow

```
Admin Panel            API Server         Windows Service        Electron App
    │                      │                      │                     │
    │  POST /api/          │                      │                     │
    │  request-screenshot  │                      │                     │
    │─────────────────────>│                      │                     │
    │                      │  HTTP (JWT auth)     │                     │
    │                      │  request-screenshot  │                     │
    │                      │─────────────────────>│                     │
    │                      │                      │  Named Pipe         │
    │                      │                      │  CAPTURE_SCREENSHOT │
    │                      │                      │────────────────────>│
    │                      │                      │                     │
    │                      │                      │                     │ desktopCapturer
    │                      │                      │                     │ .getSources({types:['screen']})
    │                      │                      │                     │
    │                      │                      │  Named Pipe         │
    │                      │                      │  SCREENSHOT_RESULT  │
    │                      │                      │<────────────────────│
    │                      │                      │                     │
    │                      │  POST /api/service/  │                     │
    │                      │  screenshot-upload   │                     │
    │                      │<─────────────────────│                     │
    │                      │                      │                     │
    │  Screenshot URL      │                      │                     │
    │<─────────────────────│                      │                     │
```

### Implementation Details

**Service side (`ServiceScreenCapture.cs`):**
- Remove all GDI+/DXGI capture code
- `CaptureAndUploadAsync()` becomes: send named pipe message, await response, upload to API
- Timeout: 10 seconds per screenshot request
- Retry: one retry on pipe timeout (Electron may be restarting)

**Electron side (`main.ts`):**
- Listen for `CAPTURE_SCREENSHOT` on named pipe
- Call `desktopCapturer.getSources({ types: ['screen'], thumbnailSize: { width: 1920, height: 1080 } })`
- Convert to JPEG buffer using `sharp` or native Canvas
- Send `SCREENSHOT_RESULT` back on named pipe
- Also POST directly to API as backup (if service upload fails)

---

## 5. Live Streaming Workflow

### Overview

For streaming, Electron's `desktopCapturer` is **insufficient** — it's capped at 30fps, uses software encoding, and adds 50-100ms+ latency. Professional streaming requires:

1. **DXGI Desktop Duplication** — GPU-native capture, ~1-2ms latency
2. **Hardware H.264/H.265 encoding** — NVENC/AMF/QuickSync, ~2-3ms latency
3. **Compressed bitstream transport** — ~5-20 Mbps over named pipe

### Architecture

```
┌───────────────────────────────────────────────────────────────┐
│                    Streaming Pipeline                          │
│                                                               │
│  Electron App                                                 │
│  ┌──────────────┐     ┌──────────────────────────────────┐   │
│  │  main.ts     │     │  NativeCaptureWorker.exe          │   │
│  │  (orchestr.) │────>│  ┌────────────┐  ┌────────────┐  │   │
│  │              │<────│  │ DXGI DD    │→ │ NVENC/MF   │  │   │
│  │              │ pipe│  │ Capture    │  │ H.264 Enc  │  │   │
│  └──────────────┘     │  └────────────┘  └────────────┘  │   │
│       │                └──────────────────────────────────┘   │
│       │ named pipe                                            │
│       ▼                                                       │
│  Windows Service                                              │
│  ┌──────────────┐     ┌──────────────┐                       │
│  │ServiceScreen │────>│ StreamFrames │────> API Server       │
│  │  Capture     │     │ (base64 POST)│     (StreamHub WS)   │
│  └──────────────┘     └──────────────┘                       │
└───────────────────────────────────────────────────────────────┘
```

### NativeCaptureWorker.exe

A standalone .NET console app that:

1. **Initializes DXGI Desktop Duplication**
   - Creates `ID3D11Device` + `IDXGIOutputDuplication`
   - Handles desktop switch, resolution change, DWM toggle

2. **Capture Loop** (dedicated thread)
   - `AcquireNextFrame(timeout)` in tight loop
   - On new frame: copy to staging texture, map to CPU memory
   - Convert BGRA → JPEG (quality-configurable) or BGRA → H.264

3. **Encoding Options** (priority order)

   | Option | CPU | Latency | Quality | Complexity |
   |---|---|---|---|---|
   | JPEG via System.Drawing | High | Medium | Good | Low |
   | JPEG via ImageSharp | Medium | Medium | Good | Low |
   | H.264 via MediaFoundation | Low | Low | Excellent | Medium |
   | H.264 via NVENC (NvEncSharp) | Very Low | Very Low | Excellent | High |

   **Phase 1 (MVP):** JPEG encoding via `System.Drawing` — simplest, already proven in codebase
   **Phase 2:** H.264 via Media Foundation — hardware-agnostic hardware encoding
   **Phase 3:** NVENC/AMF specific — maximum performance for NVIDIA/AMD GPUs

4. **Communication**
   - Control: stdin/stdout JSON (for Electron to spawn/manage)
   - Frame data: named pipe to service (high throughput)

### Streaming Protocol

```
Start Stream:
  Service → Electron:   START_STREAM { sessionId, targetFps, quality }
  Electron → Worker:    spawn NativeCaptureWorker.exe --session <id> --fps <n> --quality <n>
  Worker → Service:     Named pipe → stream frame loop

Stop Stream:
  Service → Electron:   STOP_STREAM { sessionId }
  Electron → Worker:    stdin "STOP\n" (graceful shutdown)
  Worker → Service:     STREAM_STOPPED { reason: "admin_request" }
  Worker exits
```

### Frame Rate Control

| Mode | FPS | Use Case |
|---|---|---|
| Screenshot | 1 | Periodic monitoring (every 5s default) |
| Low-bandwidth stream | 2-5 | Slow connections, many players |
| Standard stream | 10-15 | Default admin viewing |
| High-fps stream | 30 | Active investigation, evidence gathering |

The service controls FPS via the named pipe command. The worker adjusts `AcquireNextFrame` timeout accordingly.

---

## 6. Capture API Decision

| API | Screenshot | Streaming | Session 0 | Multi-Monitor | GPU Required |
|---|---|---|---|---|---|
| **Electron desktopCapturer** | Excellent | Limited (30fps cap, SW encode) | N/A (user session) | Yes | No |
| **DXGI Desktop Duplication** | Overkill | Excellent (60fps+, HW encode) | No (user session only) | Yes (1 instance/monitor) | Yes |
| **GDI CopyFromScreen** | Works | Poor (CPU-heavy, 15fps max) | No | Yes | No |

**Decision:**

| Use Case | API | Rationale |
|---|---|---|
| **Screenshot (on-demand)** | Electron `desktopCapturer` | Simple, reliable, already in user session. Single frame capture has no performance concerns. |
| **Live Stream (continuous)** | DXGI Desktop Duplication via NativeCaptureWorker | Professional-grade: GPU capture, hardware encoding, 60fps+, sub-10ms latency. |

---

## 7. Security Model

### Trust Boundaries

```
┌──────────────────────────────────────────────────────────────┐
│ TRUSTED ZONE (Service, SYSTEM privileges)                    │
│                                                              │
│  - AntiCheatWorker: detection logic, hash verification       │
│  - API authentication (service API key + JWT)                │
│  - Detections database writes                                │
│  - Screenshot/stream orchestration                           │
│                                                              │
│  The Service is the SINGLE source of truth.                  │
│  The Electron app is an UNTRUSTED capture proxy.             │
└──────────────────────────────────────────────────────────────┘
         │
         │ Named Pipe (authenticated by local-only access)
         │
┌──────────────────────────────────────────────────────────────┐
│ UNTRUSTED ZONE (Electron, user privileges)                   │
│                                                              │
│  - Screen capture (desktopCapturer / DXGI DD)                │
│  - Image encoding (JPEG/H.264)                               │
│  - Frame forwarding to service                               │
│                                                              │
│  Cannot: modify detections, bypass checks, access API keys   │
└──────────────────────────────────────────────────────────────┘
```

### Security Measures

1. **Named pipe ACL** — Service creates pipe with `WellKnownSidType.LocalSystemSid` only. Only SYSTEM and the same user can connect. No remote access.

2. **Request validation** — Service validates all pipe messages: checks requestId format, validates JSON schema, enforces timeouts.

3. **HMAC on frames** — Each frame includes an HMAC-SHA256 computed from `frameData + timestamp + sessionKey`. Service verifies before uploading. Prevents tampered/injected frames.

4. **Rate limiting** — Service enforces max 1 screenshot per 2 seconds, max stream FPS cap. Prevents the Electron app from flooding the pipe.

5. **No API keys in Electron** — The Electron app never has access to the service API key or JWT secrets. It only communicates via named pipe.

6. **Frame provenance** — Every screenshot/stream frame uploaded to the API includes `{ capturedBy: "electron-proxy", serviceSessionId, hmac }` for audit trail.

---

## 8. Performance Estimates

### Screenshot (On-Demand)

| Metric | Value |
|---|---|
| Capture time (desktopCapturer) | ~100-200ms |
| JPEG encode (1080p, quality 80) | ~10-20ms |
| Pipe transit | ~0.05ms |
| API upload (100KB JPEG) | ~50-100ms |
| **Total end-to-end** | **~200-350ms** |
| CPU usage (burst) | ~5% |
| RAM usage | ~50MB (Electron already loaded) |

### Live Stream (Continuous)

| Metric | Value (JPEG) | Value (H.264 HW) |
|---|---|---|
| Capture latency | ~1-2ms (DXGI DD) | ~1-2ms |
| Encode latency | ~5-10ms (JPEG CPU) | ~2-3ms (NVENC) |
| Pipe transit | ~0.1ms | ~0.1ms |
| Frame size (1080p) | ~50-100KB | ~5-15KB |
| Bandwidth at 10fps | 0.5-1 MB/s | 50-150 KB/s |
| Bandwidth at 30fps | 1.5-3 MB/s | 150-450 KB/s |
| CPU usage (sustained, JPEG) | ~15-25% | — |
| CPU usage (sustained, H.264 HW) | — | ~2-5% |
| RAM (NativeCaptureWorker) | ~30MB | ~50MB (GPU textures) |
| GPU usage (HW encode) | — | ~3-8% |

### Scalability (100 Concurrent Players)

| Resource | JPEG 10fps | H.264 HW 10fps |
|---|---|---|
| Total bandwidth to API | 50-100 MB/s | 5-15 MB/s |
| API receive capacity | Needs 100+ concurrent uploads | Needs 100+ concurrent uploads |
| Server storage per hour | ~180-360 GB | ~18-54 GB |
| Per-player RAM (service) | ~1MB (buffer) | ~1MB (buffer) |

**Recommendation:** Start with JPEG for screenshots. Add H.264 streaming in Phase 2 when player count demands it.

---

## 9. Implementation Phases

### Phase 1: Screenshot via Electron (Immediate Fix)

**Goal:** Get screenshots working. Replace broken Session 0 capture.

**Changes:**

1. **Service (`ServiceScreenCapture.cs`)**
   - Remove GDI+ `CopyFromScreen`, `CaptureViaDesktopSwitch`, `CaptureViaChildProcess`
   - Add `NamedPipeClientStream` connection to Electron
   - `CaptureAndUploadAsync()` sends `CAPTURE_SCREENSHOT` message, awaits response
   - Uploads JPEG to API (existing endpoint)

2. **Service (`Program.cs`)**
   - Register `NamedPipeService` as singleton
   - Start pipe server on service startup

3. **Electron (`main.ts`)**
   - Add named pipe client that connects to service pipe
   - Handle `CAPTURE_SCREENSHOT` message: call `desktopCapturer`, encode JPEG, return via pipe
   - Auto-reconnect on pipe disconnect

4. **Electron (`package.json`)**
   - Add `sharp` dependency for JPEG encoding (faster than Canvas)

**Estimated effort:** 2-3 hours
**Risk:** Low — `desktopCapturer` is proven, named pipes are simple

### Phase 2: Live Stream via Electron (Short-Term)

**Goal:** Replace current broken stream with working stream via Electron.

**Changes:**

1. **Electron** handles `START_STREAM` / `STOP_STREAM` via named pipe
2. Uses `desktopCapturer.getDisplayMedia()` → `MediaRecorder` → JPEG frames at target FPS
3. Each frame sent to service via named pipe as `STREAM_FRAME`
4. Service forwards frames to API WebSocket (existing `ScreenStreamHub`)

**Limitation:** Electron's `MediaRecorder` caps at 30fps, software encoded. Acceptable for admin monitoring.

**Estimated effort:** 3-4 hours

### Phase 3: NativeCaptureWorker (Long-Term, Optional)

**Goal:** Professional-grade streaming with hardware encoding.

**New project: `NativeCaptureWorker`** (.NET 8 console app)

**Dependencies:**
- `Vortice.DXGI` — DXGI Desktop Duplication
- `Vortice.Direct3D11` — D3D11 device/texture management
- `Vortice.DXGI` — Frame acquisition
- `System.Drawing` or `ImageSharp` — JPEG encoding (Phase 3a)
- `Lennox.NvEncSharp` or MediaFoundation — H.264 encoding (Phase 3b)

**Pipeline:**
```
DXGI DD AcquireNextFrame()
  → ID3D11Texture2D (GPU)
  → CopyResource to staging texture
  → Map to CPU (BGRA bytes)
  → JPEG encode (Phase 3a) or H.264 encode (Phase 3b)
  → Write to named pipe → Service → API
```

**Estimated effort:** 2-3 days
**Risk:** Medium — DXGI DD + encoding has many edge cases (DWM changes, resolution changes, GPU reset)

---

## 10. File Changes Summary

### Phase 1 Files

| File | Change |
|---|---|
| `src/backend/AntiCheat.Service/Services/ServiceScreenCapture.cs` | **Rewrite.** Remove capture logic. Add named pipe client. |
| `src/backend/AntiCheat.Service/Services/NamedPipeService.cs` | **New.** Named pipe client for IPC to Electron. |
| `src/backend/AntiCheat.Service/Program.cs` | Register `NamedPipeService` singleton. |
| `src/frontend/src/main/main.ts` | **Add.** Named pipe server + `desktopCapturer` handler. |
| `src/frontend/package.json` | Add `sharp` dependency. |

### Phase 2 Files

| File | Change |
|---|---|
| `src/frontend/src/main/main.ts` | Add stream control handlers (START/STOP stream, frame loop). |
| `src/backend/AntiCheat.Service/Services/ServiceScreenCapture.cs` | Add stream frame forwarding. |

### Phase 3 Files

| File | Change |
|---|---|
| `src/backend/NativeCaptureWorker/` | **New project.** DXGI DD capture + encode. |
| `src/backend/NativeCaptureWorker/CaptureEngine.cs` | DXGI DD initialization and frame loop. |
| `src/backend/NativeCaptureWorker/EncodeEngine.cs` | JPEG/H.264 encoding. |
| `src/frontend/src/main/main.ts` | Spawn/manage NativeCaptureWorker process. |

---

## 11. Risk Assessment

| Risk | Impact | Mitigation |
|---|---|---|
| Electron app not running | Screenshots/streams fail | Service detects pipe disconnect, reports "Electron offline" to API. User prompted to reopen. |
| Electron crash during capture | Single frame lost | Service timeout + retry. Worker process isolated (crash doesn't kill Electron). |
| DXGI DD access denied | Streaming fails | Fallback to Electron's desktopCapturer (lower quality but works). |
| GPU driver crash | All capture fails | Service logs error, retries after 30s. DXGI DD recreation handles `DXGI_ERROR_ACCESS_LOST`. |
| Named pipe broken pipe | IPC failure | Auto-reconnect with 5s backoff. Service queues commands during disconnect. |
| Performance on low-end GPU | High CPU for JPEG | Dynamically reduce FPS and quality. |
