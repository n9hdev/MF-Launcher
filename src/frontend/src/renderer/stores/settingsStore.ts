import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface ISettingsState {
  notifications: boolean;
  detectionAlerts: boolean;
  achievementNotifications: boolean;
  soundEffects: boolean;
  minimizeToTray: boolean;
  startOnBoot: boolean;
  autoScan: boolean;
  scanInterval: number;
  language: string;
  reducedMotion: boolean;
  compactMode: boolean;
  showFps: boolean;
  kernelLevelProtection: boolean;
  submitTelemetry: boolean;
  automaticUpdates: boolean;
  gamePath: string;
  setNotifications: (v: boolean) => void;
  setDetectionAlerts: (v: boolean) => void;
  setAchievementNotifications: (v: boolean) => void;
  setSoundEffects: (v: boolean) => void;
  setMinimizeToTray: (v: boolean) => void;
  setStartOnBoot: (v: boolean) => void;
  setAutoScan: (v: boolean) => void;
  setScanInterval: (v: number) => void;
  setLanguage: (v: string) => void;
  setReducedMotion: (v: boolean) => void;
  setCompactMode: (v: boolean) => void;
  setShowFps: (v: boolean) => void;
  setKernelLevelProtection: (v: boolean) => void;
  setSubmitTelemetry: (v: boolean) => void;
  setAutomaticUpdates: (v: boolean) => void;
  setGamePath: (v: string) => void;
}

export const useSettingsStore = create<ISettingsState>()(
  persist(
    (set) => ({
      notifications: true,
      detectionAlerts: true,
      achievementNotifications: true,
      soundEffects: true,
      minimizeToTray: true,
      startOnBoot: true,
      autoScan: true,
      scanInterval: 30,
      language: 'en',
      reducedMotion: false,
      compactMode: false,
      showFps: false,
      kernelLevelProtection: true,
      submitTelemetry: true,
      automaticUpdates: true,
      gamePath: '',
      setGamePath: (v) => set({ gamePath: v }),
      setNotifications: (v) => set({ notifications: v }),
      setDetectionAlerts: (v) => set({ detectionAlerts: v }),
      setAchievementNotifications: (v) => set({ achievementNotifications: v }),
      setSoundEffects: (v) => set({ soundEffects: v }),
      setMinimizeToTray: (v) => set({ minimizeToTray: v }),
      setStartOnBoot: (v) => set({ startOnBoot: v }),
      setAutoScan: (v) => set({ autoScan: v }),
      setScanInterval: (v) => set({ scanInterval: v }),
      setLanguage: (v) => set({ language: v }),
      setReducedMotion: (v) => set({ reducedMotion: v }),
      setCompactMode: (v) => set({ compactMode: v }),
      setShowFps: (v) => set({ showFps: v }),
      setKernelLevelProtection: (v) => set({ kernelLevelProtection: v }),
      setSubmitTelemetry: (v) => set({ submitTelemetry: v }),
      setAutomaticUpdates: (v) => set({ automaticUpdates: v }),
    }),
    { name: 'ac-settings' }
  )
);
