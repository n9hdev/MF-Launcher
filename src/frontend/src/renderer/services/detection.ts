import api from './api';

export interface IDetectionEvent {
  id: string;
  type: string;
  severity: string;
  confidence: number;
  title: string;
  description: string;
  timestamp: string;
  processName?: string;
  resolved?: boolean;
}

export interface IDetectionStatus {
  isRunning: boolean;
  lastScanTime: string | null;
  detectors: Array<{ name: string; enabled: boolean; status: string }>;
}

export interface IDetectionHealth {
  status: string;
  uptime: number;
  eventsProcessed: number;
  memoryUsage: number;
}

export const detectionApi = {
  runScan: () =>
    api.post<{ events: IDetectionEvent[] }>('/api/detection/scan'),

  getEvents: (params?: { severity?: string; limit?: number; page?: number }) =>
    api.get<IDetectionEvent[]>('/api/detection/events', { params }),

  getEventById: (id: string) =>
    api.get<IDetectionEvent>(`/api/detection/events/${id}`),

  getStatus: () =>
    api.get<IDetectionStatus>('/api/detection/status'),

  toggleDetector: (name: string, enabled: boolean) =>
    api.post(`/api/detection/detectors/${name}/toggle`, { enabled }),

  getHealth: () =>
    api.get<IDetectionHealth>('/api/detection/health'),
};
