import api from './api';

export interface ILaunchCheck {
  name: string;
  status: string;
  details?: string;
}

export interface IGameSettings {
  windowedMode: boolean;
  skipIntro: boolean;
  devConsole: boolean;
}

export interface IGameStatus {
  isRunning: boolean;
  processName?: string;
  startedAt?: string;
  uptime?: string;
}

export const gameApi = {
  launchGame: (gamePath: string) =>
    api.post<{ success: boolean }>('/api/game/launch', { gamePath }),

  stopGame: () =>
    api.post<{ message: string }>('/api/game/stop'),

  getStatus: () =>
    api.get<IGameStatus>('/api/game/status'),

  verify: () =>
    api.get<ILaunchCheck[]>('/api/game/verify'),

  getPath: () =>
    api.get<{ path: string }>('/api/game/path'),

  getSettings: () =>
    api.get<IGameSettings>('/api/game/settings'),

  updateSettings: (settings: IGameSettings) =>
    api.put<IGameSettings>('/api/game/settings', settings),
};
