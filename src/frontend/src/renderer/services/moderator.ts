import api from './api';
import type { IPlayerReport, IReportMessage } from './reports';

export interface IModeratorStats {
  openReports: number;
  activePlayers: number;
  activeAlerts: number;
  resolvedToday: number;
  avgResponseTime: number;
  reportsPerHour: number;
  banAccuracy: number;
}

export interface IActiveAlert {
  type: string;
  severity: string;
  playerName: string;
  timeAgo: string;
}

export interface IAlert {
  id: string;
  title: string;
  description: string;
  severity: string;
  confidence: number;
  timestamp: string;
  processName: string;
  resolved: boolean;
}

export interface IPlayerSearchResult {
  id: string;
  username: string;
  email?: string;
  trustScore: number;
  status: string;
  lastSeen: string;
  gameName: string;
  hoursPlayed: number;
  reportsCount: number;
  bansCount: number;
  avatar?: string;
}

export interface IPlayerDetail {
  id: string;
  username: string;
  ipAddress?: string;
  hardwareId?: string;
  serialNumber?: string;
  hardwareFingerprint?: string;
  status: string;
  gameName?: string;
  trustScore: number;
  hoursPlayed: number;
  reportsCount: number;
  bansCount: number;
  lastSeen: string;
  sessions: ISessionSummary[];
  detections: IDetectionEntry[];
}

export interface ISessionSummary {
  id: string;
  ipAddress: string;
  deviceId: string;
  createdAt: string;
  lastActivity: string;
  isActive: boolean;
}

export interface IDetectionEntry {
  id: string;
  type: string;
  severity: string;
  timestamp: string;
  description: string;
  confidence: number;
}

export const moderatorApi = {
  // ---- Stats / Alerts / Players ----
  getStats: () =>
    api.get<IModeratorStats>('/api/moderator/stats'),

  getAlerts: () =>
    api.get<IAlert[]>('/api/moderator/alerts'),

  getActiveAlerts: () =>
    api.get<IActiveAlert[]>('/api/moderator/alerts/active'),

  resolveAlert: (alertId: string) =>
    api.post(`/api/moderator/alerts/${alertId}/resolve`),

  searchPlayers: (params: { q?: string; email?: string; status?: string; minReports?: number; maxReports?: number }) =>
    api.get<IPlayerSearchResult[]>('/api/moderator/players/search', { params }),

  getPlayerDetail: (id: string) =>
    api.get<IPlayerDetail>(`/api/moderator/players/${id}`),

  getPlayerReports: (playerId: string) =>
    api.get<IPlayerReport[]>(`/api/moderator/players/${playerId}/reports`),

  // ---- Player Reports (chat system) ----
  getAllPlayerReports: (playerId?: string) =>
    api.get<IPlayerReport[]>('/api/moderator/player-reports/all', { params: { playerId } }),

  getFlaggedPlayerReports: (playerId?: string) =>
    api.get<IPlayerReport[]>('/api/moderator/player-reports/flagged', { params: { playerId } }),

  flagPlayerReport: (reportId: string, isFlagged: boolean) =>
    api.put<{ success: boolean; isFlagged: boolean }>(`/api/moderator/player-reports/${reportId}/flag`, { isFlagged }),

  getPlayerReport: (id: string) =>
    api.get<IPlayerReport>(`/api/moderator/player-reports/${id}`),

  getPlayerReportMessages: (reportId: string) =>
    api.get<{ messages: IReportMessage[] }>(`/api/moderator/player-reports/${reportId}/messages`),

  sendPlayerReportMessage: (reportId: string, message: string) =>
    api.post<{ success: boolean; message: IReportMessage }>(`/api/moderator/player-reports/${reportId}/messages`, { message }),

  sendPlayerReportAttachment: (reportId: string, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post<{ success: boolean; message: IReportMessage }>(`/api/moderator/player-reports/${reportId}/messages/attachment`, fd);
  },

  togglePlayerReportChat: (reportId: string, enabled: boolean) =>
    api.put<{ success: boolean; chatEnabled: boolean }>(`/api/moderator/player-reports/${reportId}/chat-toggle`, { chatEnabled: enabled }),

  updatePlayerReportStatus: (reportId: string, status: string) =>
    api.put<{ success: boolean }>(`/api/moderator/player-reports/${reportId}/status`, { status }),
};
