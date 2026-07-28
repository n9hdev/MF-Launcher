import { create } from 'zustand';
import type { IDetectionEvent, IProtectionStatus, ISystemHealth } from '../types/global';

interface IDetectionState {
  events: IDetectionEvent[];
  status: IProtectionStatus;
  health: ISystemHealth;
  scanRunning: boolean;
  lastScanTime: string | null;
  addEvent: (event: IDetectionEvent) => void;
  updateStatus: (partial: Partial<IProtectionStatus>) => void;
  updateHealth: (partial: Partial<ISystemHealth>) => void;
  setScanRunning: (v: boolean) => void;
  setLastScanTime: (t: string) => void;
  clearEvents: () => void;
  resolveEvent: (id: string, resolvedBy?: string) => void;
}

const defaultStatus: IProtectionStatus = {
  memoryScanner: 'active',
  processAnalyzer: 'active',
  injectionDetector: 'active',
  kernelScanner: 'active',
  yaraScanner: 'active',
  networkMonitor: 'active',
  fileIntegrity: 'active',
};

const defaultHealth: ISystemHealth = {
  cpuUsage: 23,
  memoryUsage: 412,
  networkLatency: 12,
  uptime: '14d 7h 32m',
  lastScanTime: 'Just now',
  activeModules: 7,
  totalModules: 7,
  fps: 144,
  processesMonitored: 187,
};

export const useDetectionStore = create<IDetectionState>((set) => ({
  events: [],
  status: defaultStatus,
  health: defaultHealth,
  scanRunning: false,
  lastScanTime: null,

  addEvent: (event) =>
    set((s) => ({ events: [event, ...s.events].slice(0, 500) })),
  updateStatus: (partial) =>
    set((s) => ({ status: { ...s.status, ...partial } })),
  updateHealth: (partial) =>
    set((s) => ({ health: { ...s.health, ...partial } })),
  setScanRunning: (v) => set({ scanRunning: v }),
  setLastScanTime: (t) => set({ lastScanTime: t }),
  clearEvents: () => set({ events: [] }),
  resolveEvent: (id, resolvedBy) =>
    set((s) => ({
      events: s.events.map((e) =>
        e.id === id ? { ...e, resolved: true, resolvedAt: new Date().toISOString(), resolvedBy } : e
      ),
    })),
}));
