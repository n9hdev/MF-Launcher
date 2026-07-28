import api from './api';

export interface IUserPermissions {
  permissions: string[];
  role: string;
}

export interface IPermissionDto {
  name: string;
  description: string;
  category: string;
}

export interface IAllPermissionsResponse {
  allPermissions: IPermissionDto[];
  user: IUserPermissions;
}

export interface IFeatureFlag {
  key: string;
  label: string;
  description: string;
  enabled: boolean;
}

export interface IPermissionCheck {
  permission: string;
  granted: boolean;
}

export const permissionApi = {
  getMyPermissions: () =>
    api.get<IUserPermissions>('/api/permission/my'),

  getAllPermissions: () =>
    api.get<IAllPermissionsResponse>('/api/permission/all'),

  checkPermission: (permission: string) =>
    api.post<IPermissionCheck>('/api/permission/check', { permission }),

  getFeatureFlags: () =>
    api.get<IFeatureFlag[]>('/api/permission/flags'),

  getFeatureFlag: (key: string) =>
    api.get<{ key: string; enabled: boolean }>(`/api/permission/flags/${key}`),
};
