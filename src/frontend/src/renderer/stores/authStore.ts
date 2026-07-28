import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { IUser, TrustStatus, IBanInfo } from '../types/global';

interface IAuthState {
  user: IUser | null;
  token: string | null;
  refreshToken: string | null;
  sessionId: string | null;
  deviceId: string | null;
  isAuthenticated: boolean;
  isLoggingIn: boolean;
  loginError: string | null;
  hardwareId: string | null;
  gamePath: string | null;
  restoringSession: boolean;
  trustStatus: TrustStatus;
  hwidVerified: boolean;
  isBanned: boolean;
  banInfo: IBanInfo | null;
  preLaunchCleared: boolean;
  preLaunchThreats: number;
  setAuth: (user: IUser, token: string, refreshToken: string, sessionId: string, extra?: { trustStatus?: TrustStatus; hwidVerified?: boolean; isBanned?: boolean; banInfo?: IBanInfo | null }) => void;
  setTokens: (token: string, refreshToken: string) => void;
  setDeviceId: (deviceId: string) => void;
  logout: () => void;
  setLoggingIn: (v: boolean) => void;
  setLoginError: (e: string | null) => void;
  updateTrustScore: (score: number) => void;
  updateUser: (partial: Partial<IUser>) => void;
  setHardwareId: (id: string) => void;
  setGamePath: (path: string) => void;
  setRestoringSession: (v: boolean) => void;
  setTrustStatus: (status: TrustStatus) => void;
  setHwidVerified: (v: boolean) => void;
  setBanned: (isBanned: boolean, banInfo?: IBanInfo | null) => void;
  setPreLaunchCleared: (v: boolean) => void;
  setPreLaunchThreats: (count: number) => void;
  serviceDown: boolean;
  setServiceDown: (v: boolean) => void;
}

if (typeof window !== 'undefined' && localStorage.getItem('ac-remember') === 'false' && !sessionStorage.getItem('ac-session')) {
  sessionStorage.setItem('ac-session', '1');
  localStorage.removeItem('ac-auth');
}

export const useAuthStore = create<IAuthState>()(
  persist(
    (set) => ({
      user: null,
      token: null,
      refreshToken: null,
      sessionId: null,
      deviceId: null,
      isAuthenticated: false,
      isLoggingIn: false,
      loginError: null,
      hardwareId: null,
      gamePath: null,
      restoringSession: false,
      trustStatus: 'pending' as TrustStatus,
      hwidVerified: false,
      isBanned: false,
      banInfo: null as IBanInfo | null,
      preLaunchCleared: false,
      preLaunchThreats: 0,
      serviceDown: false,

      setRestoringSession: (v) => set({ restoringSession: v }),

      setAuth: (user, token, refreshToken, sessionId, extra) => {
        sessionStorage.setItem('ac-session', '1');
        set({
          user,
          token,
          refreshToken,
          sessionId,
          isAuthenticated: true,
          loginError: null,
          trustStatus: extra?.trustStatus || user.trustStatus || 'pending',
          hwidVerified: extra?.hwidVerified ?? false,
          isBanned: extra?.isBanned ?? false,
          banInfo: extra?.banInfo ?? null,
        });
      },

      setTokens: (token, refreshToken) =>
        set({ token, refreshToken }),

      setDeviceId: (deviceId) =>
        set({ deviceId }),

      logout: () => {
        if (typeof window !== 'undefined' && window.electronAPI?.clearSessionOwner) {
          window.electronAPI.clearSessionOwner();
        }
        set({ user: null, token: null, refreshToken: null, sessionId: null, isAuthenticated: false, hardwareId: null, gamePath: null, trustStatus: 'pending', hwidVerified: false, isBanned: false, banInfo: null, preLaunchCleared: false, preLaunchThreats: 0, serviceDown: false });
      },

      setLoggingIn: (v) => set({ isLoggingIn: v }),
      setLoginError: (e) => set({ loginError: e }),

      updateTrustScore: (score) =>
        set((s) => ({ user: s.user ? { ...s.user, trustScore: score } : null })),

      updateUser: (partial) =>
        set((s) => ({ user: s.user ? { ...s.user, ...partial } : null })),

      setHardwareId: (id) => set({ hardwareId: id }),
      setGamePath: (path) => set({ gamePath: path }),
      setTrustStatus: (status) => set({ trustStatus: status }),
      setHwidVerified: (v) => set({ hwidVerified: v }),
      setBanned: (isBanned, banInfo) => set({ isBanned, banInfo: banInfo ?? null }),
      setPreLaunchCleared: (v) => set({ preLaunchCleared: v }),
      setPreLaunchThreats: (count) => set({ preLaunchThreats: count }),
      setServiceDown: (v) => set({ serviceDown: v }),
    }),
    {
      name: 'ac-auth',
      partialize: (s) => ({
        token: s.token,
        refreshToken: s.refreshToken,
        sessionId: s.sessionId,
        deviceId: s.deviceId,
        user: s.user,
        hardwareId: s.hardwareId,
        gamePath: s.gamePath,
        trustStatus: s.trustStatus,
        hwidVerified: s.hwidVerified,
        isBanned: s.isBanned,
        banInfo: s.banInfo,
      }),
    }
  )
);
