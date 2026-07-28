import { describe, it, expect, vi, beforeEach } from 'vitest';
import { gameApi } from '../../services/game';

vi.mock('../../services/api', () => {
  const mockAxios = {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    defaults: { baseURL: 'http://localhost:5000' },
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  };
  return { default: mockAxios };
});

import api from '../../services/api';

describe('gameApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('launchGame calls POST /api/game/launch with gamePath', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { success: true } });
    const result = await gameApi.launchGame('C:\\Games\\gta_sa.exe');
    expect(api.post).toHaveBeenCalledWith('/api/game/launch', { gamePath: 'C:\\Games\\gta_sa.exe' });
    expect(result.data.success).toBe(true);
  });

  it('stopGame calls POST /api/game/stop', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { message: 'Game stopped' } });
    const result = await gameApi.stopGame();
    expect(api.post).toHaveBeenCalledWith('/api/game/stop');
    expect(result.data.message).toBe('Game stopped');
  });

  it('getStatus calls GET /api/game/status', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { isRunning: false } });
    const result = await gameApi.getStatus();
    expect(api.get).toHaveBeenCalledWith('/api/game/status');
    expect(result.data.isRunning).toBe(false);
  });

  it('verify calls GET /api/game/verify', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [{ name: 'MTA', status: 'ok' }] });
    const result = await gameApi.verify();
    expect(api.get).toHaveBeenCalledWith('/api/game/verify');
    expect(result.data[0].status).toBe('ok');
  });

  it('getSettings calls GET /api/game/settings', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { windowedMode: true, skipIntro: false, devConsole: false } });
    await gameApi.getSettings();
    expect(api.get).toHaveBeenCalledWith('/api/game/settings');
  });

  it('updateSettings calls PUT /api/game/settings', async () => {
    vi.mocked(api.put).mockResolvedValue({ data: {} });
    await gameApi.updateSettings({ windowedMode: true, skipIntro: false, devConsole: true });
    expect(api.put).toHaveBeenCalledWith('/api/game/settings', { windowedMode: true, skipIntro: false, devConsole: true });
  });

  it('getPath calls GET /api/game/path', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { path: 'C:\\Games' } });
    const result = await gameApi.getPath();
    expect(api.get).toHaveBeenCalledWith('/api/game/path');
    expect(result.data.path).toBe('C:\\Games');
  });

  it('handles API error', async () => {
    vi.mocked(api.post).mockRejectedValue(new Error('Game not found'));
    await expect(gameApi.launchGame('bad.exe')).rejects.toThrow('Game not found');
  });
});
