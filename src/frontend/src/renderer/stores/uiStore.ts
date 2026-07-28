import { create } from 'zustand';
import type { IToast } from '../types/global';

interface IUpdateInfo {
  hasUpdate: boolean;
  currentVersion: string;
  latestVersion: string;
  downloadUrl: string;
  sha256: string;
  size: number;
  isCritical: boolean;
  changelog: string;
  error?: string;
}

interface IUIState {
  sidebarOpen: boolean;
  sidebarCollapsed: boolean;
  infoDrawerOpen: boolean;
  infoDrawerContent: React.ReactNode | null;
  commandPaletteOpen: boolean;
  searchOpen: boolean;
  contextMenu: { x: number; y: number; items: { label: string; icon?: string; action: () => void }[] } | null;
  toasts: IToast[];
  activeModal: string | null;
  criticalUpdate: IUpdateInfo | null;
  toggleSidebar: () => void;
  setSidebarCollapsed: (v: boolean) => void;
  toggleInfoDrawer: () => void;
  setInfoDrawerContent: (content: React.ReactNode | null) => void;
  toggleCommandPalette: () => void;
  toggleSearch: () => void;
  openContextMenu: (x: number, y: number, items: { label: string; icon?: string; action: () => void }[]) => void;
  closeContextMenu: () => void;
  addToast: (toast: Omit<IToast, 'id'>) => void;
  removeToast: (id: string) => void;
  setActiveModal: (id: string | null) => void;
  setCriticalUpdate: (info: IUpdateInfo | null) => void;
}

let toastCounter = 0;

export const useUIStore = create<IUIState>((set) => ({
  sidebarOpen: true,
  sidebarCollapsed: false,
  infoDrawerOpen: false,
  infoDrawerContent: null,
  commandPaletteOpen: false,
  searchOpen: false,
  contextMenu: null,
  toasts: [],
  activeModal: null,
  criticalUpdate: null,

  toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),
  setSidebarCollapsed: (v) => set({ sidebarCollapsed: v }),
  toggleInfoDrawer: () => set((s) => ({ infoDrawerOpen: !s.infoDrawerOpen })),
  setInfoDrawerContent: (content) => set({ infoDrawerContent: content, infoDrawerOpen: content !== null }),

  toggleCommandPalette: () => set((s) => ({ commandPaletteOpen: !s.commandPaletteOpen })),
  toggleSearch: () => set((s) => ({ searchOpen: !s.searchOpen })),

  openContextMenu: (x, y, items) => set({ contextMenu: { x, y, items } }),
  closeContextMenu: () => set({ contextMenu: null }),

  addToast: (toast) => {
    const id = `toast-${++toastCounter}`;
    set((s) => ({ toasts: [...s.toasts, { ...toast, id }] }));
    setTimeout(() => {
      set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) }));
    }, toast.duration ?? 4000);
  },

  removeToast: (id) => set((s) => ({ toasts: s.toasts.filter((t) => t.id !== id) })),
  setActiveModal: (id) => set({ activeModal: id }),
  setCriticalUpdate: (info) => set({ criticalUpdate: info }),
}));
