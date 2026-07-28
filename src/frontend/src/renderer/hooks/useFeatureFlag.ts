import { usePermissionStore } from '../stores/permissionStore';

export function useFeatureFlag(key: string): boolean {
  return usePermissionStore((s) => {
    const flag = s.featureFlags.find((f) => f.key === key);
    return flag?.enabled ?? false;
  });
}
