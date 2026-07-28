import { app, BrowserWindow, ipcMain, Tray, Menu, nativeImage, desktopCapturer, screen } from 'electron';
import path from 'path';
import * as net from 'net';
import { setupIpcHandlers } from './ipc/handlers';
import { createTray } from './tray/manager';

let mainWindow: BrowserWindow | null = null;
let tray: Tray | null = null;

const isDev = process.env.NODE_ENV === 'development';
const devServerPort = process.env.VITE_PORT || '5173';

const PIPE_NAME = '\\\\.\\pipe\\mf-anticheat-capture';
let pipeClient: net.Socket | null = null;
let pipeReconnectTimer: NodeJS.Timeout | null = null;
let pipeConnected = false;

// Stream state
let activeStreamSessionId: string | null = null;
let streamInterval: NodeJS.Timeout | null = null;
let streamTargetFps = 2;
let streamJpegQuality = 60;

function createWindow(): void {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 800,
    minWidth: 1024,
    minHeight: 700,
    frame: false,
    transparent: true,
    icon: path.join(__dirname, '../../public/icon.ico'),
    webPreferences: {
      preload: path.join(__dirname, '../preload/preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false,
    },
  });

  if (isDev) {
    mainWindow.loadURL(`http://127.0.0.1:${devServerPort}`);
    mainWindow.webContents.openDevTools();
  } else {
    mainWindow.loadFile(path.join(__dirname, '../renderer/index.html'));
  }

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

// ─── Named Pipe IPC ───────────────────────────────────────────────────

function connectToServicePipe(): void {
  if (pipeClient) {
    try { pipeClient.destroy(); } catch {}
    pipeClient = null;
  }

  console.log('[IPC] Connecting to service pipe:', PIPE_NAME);

  pipeClient = net.createConnection(PIPE_NAME);

  pipeClient.on('connect', () => {
    console.log('[IPC] Connected to service pipe');
    pipeConnected = true;

    // Send status/capabilities
    const screens = screen?.getAllDisplays?.() ?? [];
    sendPipeMessage({
      type: 'status',
      version: app.getVersion(),
      captureBackend: 'desktopCapturer',
      gpuAvailable: true,
      screenCount: screens.length || 1,
    });
  });

  pipeClient.on('data', (data: Buffer) => {
    handlePipeData(data);
  });

  pipeClient.on('error', (err) => {
    console.error('[IPC] Pipe error:', err.message);
    pipeConnected = false;
  });

  pipeClient.on('close', () => {
    console.log('[IPC] Pipe closed, reconnecting in 3s...');
    pipeConnected = false;
    pipeClient = null;
    schedulePipeReconnect();
  });
}

function schedulePipeReconnect(): void {
  if (pipeReconnectTimer) return;
  pipeReconnectTimer = setTimeout(() => {
    pipeReconnectTimer = null;
    connectToServicePipe();
  }, 3000);
}

function sendPipeMessage(msg: any): void {
  if (!pipeClient || !pipeClient.writable) return;
  const json = JSON.stringify(msg);
  const jsonBuf = Buffer.from(json, 'utf8');
  const header = Buffer.alloc(4);
  header.writeUInt32LE(jsonBuf.length, 0);
  try {
    pipeClient.write(Buffer.concat([header, jsonBuf]));
  } catch (err) {
    console.error('[IPC] Failed to send message:', err);
  }
}

// Pipe data buffering
let pipeBuffer = Buffer.alloc(0);

function handlePipeData(data: Buffer): void {
  pipeBuffer = Buffer.concat([pipeBuffer, data]);

  while (pipeBuffer.length >= 4) {
    const msgLen = pipeBuffer.readUInt32LE(0);
    if (pipeBuffer.length < 4 + msgLen) break; // incomplete message

    const msgBuf = pipeBuffer.subarray(4, 4 + msgLen);
    pipeBuffer = pipeBuffer.subarray(4 + msgLen);

    try {
      const msg = JSON.parse(msgBuf.toString('utf8'));
      handlePipeMessage(msg);
    } catch (err) {
      console.error('[IPC] Failed to parse pipe message:', err);
    }
  }
}

function handlePipeMessage(msg: any): void {
  switch (msg.type) {
    case 'capture_screenshot':
      handleScreenshotRequest(msg);
      break;
    case 'start_stream':
      handleStreamStart(msg);
      break;
    case 'stop_stream':
      handleStreamStop(msg);
      break;
    case 'heartbeat':
      sendPipeMessage({ type: 'heartbeat_ack' });
      break;
    default:
      console.log('[IPC] Unknown message type:', msg.type);
  }
}

// ─── Screenshot Capture ───────────────────────────────────────────────

async function handleScreenshotRequest(msg: any): Promise<void> {
  const requestId = msg.requestId;
  const quality = msg.quality ?? 80;

  if (!requestId) {
    console.error('[Capture] Screenshot request missing requestId');
    return;
  }

  try {
    console.log(`[Capture] Screenshot requested (id=${requestId})`);

    const sources = await desktopCapturer.getSources({
      types: ['screen'],
      thumbnailSize: { width: 1920, height: 1080 },
    });

    if (!sources || sources.length === 0) {
      sendPipeMessage({
        type: 'screenshot_result',
        requestId,
        success: false,
        error: 'No screen sources available',
      });
      return;
    }

    // Capture primary screen
    const source = sources[0];
    const thumbnail = source.thumbnail;

    if (thumbnail.isEmpty()) {
      sendPipeMessage({
        type: 'screenshot_result',
        requestId,
        success: false,
        error: 'Captured thumbnail is empty',
      });
      return;
    }

    // Convert to JPEG buffer using nativeImage
    const jpegBuffer = thumbnail.toJPEG(quality);

    const base64 = jpegBuffer.toString('base64');
    const size = thumbnail.getSize();

    console.log(`[Capture] Screenshot captured: ${size.width}x${size.height}, ${jpegBuffer.length} bytes`);

    sendPipeMessage({
      type: 'screenshot_result',
      requestId,
      success: true,
      imageData: base64,
      width: size.width,
      height: size.height,
    });
  } catch (err: any) {
    console.error('[Capture] Screenshot failed:', err);
    sendPipeMessage({
      type: 'screenshot_result',
      requestId,
      success: false,
      error: err.message || 'Unknown capture error',
    });
  }
}

// ─── Live Stream ──────────────────────────────────────────────────────

function handleStreamStart(msg: any): void {
  const { requestId, sessionId, targetFps = 2, jpegQuality = 60 } = msg;

  // Stop existing stream if any
  if (streamInterval) {
    clearInterval(streamInterval);
    streamInterval = null;
  }

  activeStreamSessionId = sessionId;
  streamTargetFps = Math.max(1, Math.min(30, targetFps));
  streamJpegQuality = Math.max(10, Math.min(90, jpegQuality));

  const intervalMs = Math.round(1000 / streamTargetFps);

  console.log(`[Stream] Starting: session=${sessionId}, fps=${streamTargetFps}, interval=${intervalMs}ms`);

  streamInterval = setInterval(async () => {
    await captureStreamFrame(sessionId);
  }, intervalMs);

  sendPipeMessage({
    type: 'stream_started',
    requestId,
    success: true,
  });
}

function handleStreamStop(msg: any): void {
  if (streamInterval) {
    clearInterval(streamInterval);
    streamInterval = null;
  }
  activeStreamSessionId = null;
  console.log('[Stream] Stopped');
}

async function captureStreamFrame(sessionId: string): Promise<void> {
  try {
    const sources = await desktopCapturer.getSources({
      types: ['screen'],
      thumbnailSize: { width: 1280, height: 720 },
    });

    if (!sources || sources.length === 0) return;

    const thumbnail = sources[0].thumbnail;
    if (thumbnail.isEmpty()) return;

    const jpegBuffer = thumbnail.toJPEG(streamJpegQuality);
    const size = thumbnail.getSize();

    sendPipeMessage({
      type: 'stream_frame',
      requestId: sessionId,
      imageData: jpegBuffer.toString('base64'),
      width: size.width,
      height: size.height,
      timestamp: Date.now(),
    });
  } catch {
    // Silently skip failed frames
  }
}

// ─── Electron App Lifecycle ───────────────────────────────────────────

app.whenReady().then(() => {
  createWindow();
  setupIpcHandlers(ipcMain);
  tray = createTray(mainWindow);

  // Connect to service named pipe
  connectToServicePipe();

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  // Stop stream before exiting
  if (streamInterval) {
    clearInterval(streamInterval);
    streamInterval = null;
  }

  // Disconnect pipe
  if (pipeClient) {
    try { pipeClient.destroy(); } catch {}
    pipeClient = null;
  }
  if (pipeReconnectTimer) {
    clearTimeout(pipeReconnectTimer);
    pipeReconnectTimer = null;
  }

  if (process.platform !== 'darwin') {
    app.quit();
  }
});
