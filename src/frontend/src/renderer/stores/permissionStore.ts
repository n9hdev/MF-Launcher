import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { IUserPermissions, IFeatureFlag } from '../services/permission';

interface IPermissionState {
  permissions: string[];
  role: string;
  featureFlags: IFeatureFlag[];
  loaded: boolean;
  setPermissions: (data: IUserPermissions) => void;
  setFeatureFlags: (flags: IFeatureFlag[]) => void;
  hasPermission: (permission: string) => boolean;
  isFeatureEnabled: (key: string) => boolean;
  reset: () => void;
}

export const usePermissionStore = create<IPermissionState>()(
  persist(
    (set, get) => ({
      permissions: [],
      role: '',
      featureFlags: [],
      loaded: false,

      setPermissions: (data) =>
        set({ permissions: data.permissions, role: data.role, loaded: true }),

      setFeatureFlags: (flags) =>
        set({ featureFlags: flags }),

      hasPermission: (permission) =>
        get().permissions.includes(permission),

      isFeatureEnabled: (key) => {
        const flag = get().featureFlags.find((f) => f.key === key);
        return flag?.enabled ?? false;
      },

      reset: () =>
        set({ permissions: [], role: '', featureFlags: [], loaded: false }),
    }),
    {
      name: 'ac-permissions',
      partialize: (s) => ({
        permissions: s.permissions,
        role: s.role,
        featureFlags: s.featureFlags,
      }),
    }
  )
);
