import { IpcMain, BrowserWindow, dialog, app } from 'electron';
import * as fs from 'fs';
import * as path from 'path';
import { spawn, ChildProcess } from 'child_process';
import { checkForUpdates, downloadAndVerifyUpdate, installUpdate } from '../updater/service';

let gameProcess: ChildProcess | null = null;
let gameStartTime: number | null = null;

function getWindow(event: Electron.IpcMainInvokeEvent): BrowserWindow | null {
  return BrowserWindow.fromWebContents(event.sender);
}

export function setupIpcHandlers(ipcMain: IpcMain): void {
  ipcMain.handle('window:minimize', (event) => {
    getWindow(event)?.minimize();
  });

  ipcMain.handle('window:maximize', (event) => {
    const win = getWindow(event);
    if (win) {
      if (win.isMaximized()) {
        win.unmaximize();
      } else {
        win.maximize();
      }
    }
  });

  ipcMain.handle('window:close', (event) => {
    getWindow(event)?.close();
  });

  ipcMain.handle('app:getVersion', () => {
    return app.getVersion();
  });

  ipcMain.handle('session:writeOwner', (_event, userId: string) => {
    const programData = process.env.PROGRAMDATA || 'C:\\ProgramData';
    const dir = path.join(programData, 'AntiCheat');
    try {
      if (!fs.existsSync(dir)) fs.mkdirSync(dir, { recursive: true });
      fs.writeFileSync(path.join(dir, 'session_owner.txt'), userId, 'utf-8');
    } catch {}
  });

  ipcMain.handle('session:clearOwner', () => {
    const programData = process.env.PROGRAMDATA || 'C:\\ProgramData';
    const file = path.join(programData, 'AntiCheat', 'session_owner.txt');
    try { if (fs.existsSync(file)) fs.unlinkSync(file); } catch {}
  });

  ipcMain.handle('hwid:read', () => {
    const programData = process.env.PROGRAMDATA || 'C:\\ProgramData';
    const hwidFile = path.join(programData, 'AntiCheat', 'hwid.txt');
    try {
      if (fs.existsSync(hwidFile)) {
        return fs.readFileSync(hwidFile, 'utf-8').trim();
      }
    } catch {}
    return null;
  });

  ipcMain.handle('dialog:openFile', async (_event, options: Electron.OpenDialogOptions) => {
    const win = BrowserWindow.getFocusedWindow();
    if (!win) return null;
    const result = await dialog.showOpenDialog(win, options);
    if (result.canceled || result.filePaths.length === 0) return null;
    return result.filePaths[0];
  });

  ipcMain.handle('game:launch', async (_event, exePath: string) => {
    if (gameProcess && !gameProcess.killed) {
      return { success: false, error: 'Game already running' };
    }
    if (!exePath || !fs.existsSync(exePath)) {
      return { success: false, error: 'Executable not found' };
    }
    try {
      gameProcess = spawn(exePath, [], {
        detached: false,
        stdio: 'ignore',
      });
      gameStartTime = Date.now();
      gameProcess.on('exit', () => {
        gameProcess = null;
        gameStartTime = null;
      });
      gameProcess.on('error', (err) => {
        gameProcess = null;
        gameStartTime = null;
      });
      return { success: true };
    } catch (err: any) {
      return { success: false, error: err.message };
    }
  });

  ipcMain.handle('game:stop', async () => {
    if (gameProcess && !gameProcess.killed) {
      try {
        gameProcess.kill('SIGTERM');
        setTimeout(() => {
          if (gameProcess && !gameProcess.killed) {
            gameProcess.kill('SIGKILL');
          }
        }, 5000);
      } catch {}
      gameProcess = null;
      gameStartTime = null;
    }
    return { success: true };
  });

  ipcMain.handle('game:status', async () => {
    const running = gameProcess !== null && !gameProcess.killed;
    return {
      isRunning: running,
      startedAt: gameStartTime ? new Date(gameStartTime).toISOString() : null,
      uptime: running && gameStartTime ? Math.floor((Date.now() - gameStartTime) / 1000) : 0,
    };
  });

  // --- Secure Update IPC handlers ---
  ipcMain.handle('update:check', async () => {
    return await checkForUpdates();
  });

  ipcMain.handle('update:download', async (_event, downloadUrl: string, fallbackDownloadUrl: string, expectedSha256: string, expectedSize: number) => {
    const win = BrowserWindow.getFocusedWindow();
    const result = await downloadAndVerifyUpdate(downloadUrl, fallbackDownloadUrl, expectedSha256, expectedSize, (percent) => {
      if (win) {
        win.webContents.send('update:download-progress', { percent });
      }
    });
    if (result.success) {
      installUpdate(result.installerPath);
    }
    return result;
  });
}
