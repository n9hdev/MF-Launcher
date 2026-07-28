export interface IUpdateCheckResult {
  hasUpdate: boolean;
  currentVersion: string;
  latestVersion: string;
  downloadUrl: string;
  fallbackDownloadUrl: string;
  sha256: string;
  size: number;
  changelog: string;
  releaseDate: string;
  isCritical: boolean;
  error?: string;
}

export interface IUpdateDownloadProgress {
  percent: number;
}

export interface IElectronAPI {
  minimize: () => Promise<void>;
  maximize: () => Promise<void>;
  close: () => Promise<void>;
  getVersion: () => Promise<string>;
  openFilePicker: (options: { filters?: { name: string; extensions: string[] }[] }) => Promise<string | null>;
  readHwid: () => Promise<string | null>;
  writeSessionOwner: (userId: string) => Promise<void>;
  clearSessionOwner: () => Promise<void>;
  onUpdateAvailable: (callback: (info: unknown) => void) => void;
  onUpdateProgress: (callback: (progress: unknown) => void) => void;
  onUpdateDownloaded: (callback: () => void) => void;
  updateProtectionStatus: (status: string) => void;
  launchGame: (exePath: string) => Promise<{ success: boolean; error?: string }>;
  stopGame: () => Promise<{ success: boolean }>;
  getGameStatus: () => Promise<{ isRunning: boolean; startedAt: string | null; uptime: number }>;
  checkForUpdates: () => Promise<IUpdateCheckResult>;
  installUpdate: (downloadUrl: string, fallbackDownloadUrl: string, expectedSha256: string, expectedSize: number) => Promise<{ success: boolean; installerPath?: string; error?: string }>;
  onUpdateDownloadProgress: (callback: (progress: IUpdateDownloadProgress) => void) => void;
}

declare global {
  interface Window {
    electronAPI: IElectronAPI;
  }
}
