import api from './api';

export interface ILoginRequest {
  username: string;
  password: string;
  deviceId?: string;
}

export interface IUserProfile {
  id: string;
  username: string;
  displayName: string;
  role: string;
  trustScore: number;
  trustStatus: string;
  level: number;
  status: string;
  avatar?: string;
  email?: string;
  xp: number;
  nextLevelXp: number;
  hardwareId?: string;
  gamePath?: string;
  createdAt: string;
  lastLogin: string;
}

export interface IBanInfoDto {
  id: string;
  reason: string;
  type: string;
  issuedBy: string;
  issuedAt: string;
  proofUrl?: string;
  durationHours: number;
  bannedAt: string;
}

export interface IHardwareVerificationResult {
  isVerified: boolean;
  hwidStored: boolean;
  currentHwid: string | null;
  storedHwid: string | null;
  matches: boolean;
}

export interface ILoginResponse {
  user: IUserProfile;
  accessToken: string;
  refreshToken: string;
  sessionId: string;
  trustStatus: string;
  hwidVerified: boolean;
  isBanned: boolean;
  banInfo?: IBanInfoDto;
}

export interface IDeviceRegistrationRequest {
  deviceId: string;
  deviceName: string;
  osVersion: string;
  fingerprint: string;
}

export interface IDeviceRegistrationResponse {
  success: boolean;
  deviceId: string;
  trustScore: number;
  requiresVerification: boolean;
}

export interface ISessionInfo {
  sessionId: string;
  userId: string;
  deviceId: string;
  ipAddress: string;
  createdAt: string;
  lastActivity: string | null;
  isActive: boolean;
}

export interface IRegisterRequest {
  username: string;
  password: string;
  displayName: string;
  email?: string;
  hardwareId?: string;
}

export const authApi = {
  login: (data: ILoginRequest) =>
    api.post<ILoginResponse>('/api/auth/login', data),

  register: (data: IRegisterRequest) =>
    api.post<ILoginResponse>('/api/auth/register', data),

  refresh: (refreshToken: string) =>
    api.post<{ accessToken: string; refreshToken: string }>('/api/auth/refresh', { refreshToken }),

  logout: (sessionId: string) =>
    api.post('/api/auth/logout', { sessionId }),

  getMe: () =>
    api.get<IUserProfile>('/api/auth/me'),

  getProfile: () =>
    api.get<IUserProfile>('/api/auth/profile'),

  updateProfile: (data: { gamePath?: string; hardwareId?: string }) =>
    api.put<IUserProfile>('/api/auth/profile', data),

  registerDevice: (data: IDeviceRegistrationRequest) =>
    api.post<IDeviceRegistrationResponse>('/api/auth/devices/register', data),

  getSessions: () =>
    api.get<ISessionInfo[]>('/api/auth/sessions'),

  terminateSession: (sessionId: string) =>
    api.post(`/api/auth/sessions/${sessionId}/terminate`),

  getTrustStatus: () =>
    api.get<{ trustStatus: string }>('/api/auth/trust-status'),

  verifyHardware: () =>
    api.get<IHardwareVerificationResult>('/api/auth/verify-hardware'),

  getActiveBan: () =>
    api.get<{ banned: boolean; ban?: IBanInfoDto }>('/api/auth/bans/active'),

  submitAppeal: (banId: string, message: string) =>
    api.post(`/api/auth/bans/${banId}/appeal`, { message }),

  getMyAppeal: () =>
    api.get<{ hasAppeal: boolean; appeal?: unknown }>('/api/auth/bans/appeal'),

  sendAppealMessage: (message: string) =>
    api.post<{ success: boolean; message: unknown }>('/api/auth/bans/appeal/messages', { message }),

  changePassword: (currentPassword: string, newPassword: string) =>
    api.post<{ success: boolean }>('/api/auth/change-password', { currentPassword, newPassword }),

  getIdentity: () =>
    api.get<{ ip: string; hardwareId: string; serialNumber: string }>('/api/auth/identity'),
};
