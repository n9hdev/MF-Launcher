import { create } from 'zustand';

interface ISessionState {
  gameRunning: boolean;
  gameName: string | null;
  gamePath: string | null;
  sessionStartTime: string | null;
  sessionDuration: number;
  protectionActive: boolean;
  setGameRunning: (running: boolean, name?: string, path?: string) => void;
  stopGame: () => void;
  setProtectionActive: (v: boolean) => void;
  tickDuration: () => void;
}

export const useSessionStore = create<ISessionState>((set) => ({
  gameRunning: false,
  gameName: null,
  gamePath: null,
  sessionStartTime: null,
  sessionDuration: 0,
  protectionActive: true,

  setGameRunning: (running, name, path) =>
    set({
      gameRunning: running,
      gameName: name ?? null,
      gamePath: path ?? null,
      sessionStartTime: running ? new Date().toISOString() : null,
      sessionDuration: running ? 0 : 0,
    }),

  stopGame: () =>
    set({ gameRunning: false, gameName: null, gamePath: null }),

  setProtectionActive: (v) => set({ protectionActive: v }),

  tickDuration: () =>
    set((s) => ({ sessionDuration: s.sessionDuration + 1 })),
}));
