import api from './api';

export interface IAdminStats {
  activeBans: number;
  pendingAppeals: number;
  detectionRate: number;
  totalPlayers: number;
  permanentBans: number;
  temporaryBans: number;
}

export interface IDetectorPerformance {
  name: string;
  detections: number;
  accuracy: number;
  status: string;
}

export interface IAdminBanEntry {
  id: string;
  player: string;
  reason: string;
  type: string;
  issuedBy: string;
  issuedAt: string;
  active: boolean;
  appeals: number;
  [key: string]: unknown;
}

export interface IAppealMessage {
  id: string;
  appealId: string;
  senderId: string;
  senderName: string;
  message: string;
  createdAt: string;
}

export interface IAdminAppeal {
  id: string;
  player: string;
  playerId?: string;
  banId?: string;
  reason: string;
  banType: string;
  status: string;
  date: string;
  reviewer: string;
  messages?: IAppealMessage[];
  [key: string]: unknown;
}

export interface IWhitelistEntry {
  id: string;
  entry: string;
  type: string;
  addedBy: string;
  addedAt: string;
  reason: string;
  [key: string]: unknown;
}

export interface IWeeklyActivity {
  day: string;
  scans: number;
  threats: number;
  players: number;
}

export interface IThreatDistribution {
  type: string;
  count: number;
  pct: number;
}

export interface ITopReport {
  player: string;
  reports: number;
  action: string;
}

export interface ICreateBanRequest { player: string; reason: string; type: string; duration?: string; }

export interface IUpdateBanRequest { reason?: string; type?: string; active?: boolean; }

export interface IAddWhitelistEntryRequest { entry: string; type: string; reason: string; }

export interface IUpdateWhitelistEntryRequest { entry?: string; type?: string; reason?: string; }

export interface IUpdateAppealStatusRequest { status: string; reviewer: string; }

export const adminApi = {
  getStats: () =>
    api.get<IAdminStats>('/api/admin/stats'),

  getDetectors: () =>
    api.get<IDetectorPerformance[]>('/api/admin/detectors'),

  getBans: () =>
    api.get<IAdminBanEntry[]>('/api/admin/bans'),

  getBanById: (id: string) =>
    api.get<IAdminBanEntry>(`/api/admin/bans/${id}`),

  createBan: (request: ICreateBanRequest) =>
    api.post<IAdminBanEntry>('/api/admin/bans', request),

  updateBan: (id: string, request: IUpdateBanRequest) =>
    api.put<IAdminBanEntry>(`/api/admin/bans/${id}`, request),

  revokeBan: (id: string) =>
    api.delete<{ success: boolean }>(`/api/admin/bans/${id}`),

  getAppeals: () =>
    api.get<IAdminAppeal[]>('/api/admin/appeals'),

  getAppealById: (id: string) =>
    api.get<IAdminAppeal>(`/api/admin/appeals/${id}`),

  updateAppealStatus: (id: string, request: IUpdateAppealStatusRequest) =>
    api.put<IAdminAppeal>(`/api/admin/appeals/${id}`, request),

  getAppealMessages: (appealId: string) =>
    api.get<{ messages: IAppealMessage[] }>(`/api/admin/appeals/${appealId}/messages`),

  sendAppealMessage: (appealId: string, message: string) =>
    api.post<IAppealMessage>(`/api/admin/appeals/${appealId}/messages`, { message }),

  getWhitelist: () =>
    api.get<IWhitelistEntry[]>('/api/admin/whitelist'),

  addWhitelistEntry: (request: IAddWhitelistEntryRequest) =>
    api.post<IWhitelistEntry>('/api/admin/whitelist', request),

  removeWhitelistEntry: (id: string) =>
    api.delete<{ success: boolean }>(`/api/admin/whitelist/${id}`),

  updateWhitelistEntry: (id: string, request: IUpdateWhitelistEntryRequest) =>
    api.put<IWhitelistEntry>(`/api/admin/whitelist/${id}`, request),

  getWeeklyActivity: () =>
    api.get<IWeeklyActivity[]>('/api/admin/analytics/weekly'),

  getThreatDistribution: () =>
    api.get<IThreatDistribution[]>('/api/admin/analytics/threats'),

  getTopReports: () =>
    api.get<ITopReport[]>('/api/admin/analytics/top-reports'),
};
