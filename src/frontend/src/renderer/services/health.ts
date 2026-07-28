import api from './api';

export interface IHealthStatus {
  healthy: boolean;
  serviceStatus: string;
  message: string;
}

export const healthApi = {
  getStatus: () =>
    api.get<IHealthStatus>('/api/health/status'),

  restartService: () =>
    api.post<{ message: string }>('/api/health/restart'),
};
