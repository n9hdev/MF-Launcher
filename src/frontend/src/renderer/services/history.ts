import api from './api';

export interface ITimelineEvent {
  id: string;
  type: string;
  title: string;
  description: string;
  timestamp: string;
  severity: string;
  category?: string;
  confidence?: number;
  count?: number;
}

export interface IHistorySummary {
  critical: number;
  high: number;
  medium: number;
  low: number;
  info: number;
}

export interface IDetectionStats {
  totalScans: number;
  threatsFound: number;
  falsePositives: number;
  uptimePercent: number;
  cleanScans: number;
}

export const historyApi = {
  getTimeline: (params?: { severity?: string; category?: string; search?: string; page?: number; limit?: number }) =>
    api.get<ITimelineEvent[]>('/api/history', { params }),

  getSummary: () =>
    api.get<IHistorySummary>('/api/history/summary'),

  getStats: () =>
    api.get<IDetectionStats>('/api/history/stats'),
};
