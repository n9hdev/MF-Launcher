import { usePermissionStore } from '../stores/permissionStore';

export function usePermission(permission: string): boolean {
  return usePermissionStore((s) => s.permissions.includes(permission));
}

export function useHasAnyPermission(permissions: string[]): boolean {
  return usePermissionStore((s) => permissions.some((p) => s.permissions.includes(p)));
}

export function useHasAllPermissions(permissions: string[]): boolean {
  return usePermissionStore((s) => permissions.every((p) => s.permissions.includes(p)));
}
