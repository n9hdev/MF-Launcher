import { contextBridge, ipcRenderer } from 'electron';

contextBridge.exposeInMainWorld('electronAPI', {
  minimize: () => ipcRenderer.invoke('window:minimize'),
  maximize: () => ipcRenderer.invoke('window:maximize'),
  close: () => ipcRenderer.invoke('window:close'),
  getVersion: () => ipcRenderer.invoke('app:getVersion'),
  readHwid: () => ipcRenderer.invoke('hwid:read'),
  writeSessionOwner: (userId: string) => ipcRenderer.invoke('session:writeOwner', userId),
  clearSessionOwner: () => ipcRenderer.invoke('session:clearOwner'),
  openFilePicker: (options: { filters?: { name: string; extensions: string[] }[] }) =>
    ipcRenderer.invoke('dialog:openFile', {
      properties: ['openFile'],
      filters: options.filters,
    }),
  onUpdateAvailable: (callback: (info: unknown) => void) => {
    ipcRenderer.on('update:available', (_event, info) => callback(info));
  },
  onUpdateProgress: (callback: (progress: unknown) => void) => {
    ipcRenderer.on('update:progress', (_event, progress) => callback(progress));
  },
  onUpdateDownloaded: (callback: () => void) => {
    ipcRenderer.on('update:downloaded', () => callback());
  },
  updateProtectionStatus: (status: string) => {
    ipcRenderer.send('tray:protection-status', status);
  },
  launchGame: (exePath: string) => ipcRenderer.invoke('game:launch', exePath),
  stopGame: () => ipcRenderer.invoke('game:stop'),
  getGameStatus: () => ipcRenderer.invoke('game:status'),
  checkForUpdates: () => ipcRenderer.invoke('update:check'),
  installUpdate: (downloadUrl: string, fallbackDownloadUrl: string, expectedSha256: string, expectedSize: number) =>
    ipcRenderer.invoke('update:download', downloadUrl, fallbackDownloadUrl, expectedSha256, expectedSize),
  onUpdateDownloadProgress: (callback: (progress: { percent: number }) => void) => {
    ipcRenderer.on('update:download-progress', (_event, progress) => callback(progress));
  },
});
