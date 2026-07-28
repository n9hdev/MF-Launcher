import api from './api';

export interface IActivityEvent {
  id: string;
  type: string;
  title: string;
  description: string;
  timestamp: string;
  severity?: string;
  icon?: string;
}

export const activityApi = {
  getRecentActivity: (count?: number) =>
    api.get<IActivityEvent[]>('/api/activity', { params: { count } }),
};
