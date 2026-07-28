import { BrowserWindow } from 'electron';

export function getMainWindow(): BrowserWindow | null {
  const windows = BrowserWindow.getAllWindows();
  return windows.length > 0 ? windows[0] : null;
}

export function showMainWindow(): void {
  const win = getMainWindow();
  if (win) {
    win.show();
    win.focus();
  }
}

export function sendToRenderer(channel: string, ...args: unknown[]): void {
  const win = getMainWindow();
  if (win && !win.isDestroyed()) {
    win.webContents.send(channel, ...args);
  }
}
