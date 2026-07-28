import { describe, it, expect, vi, beforeEach } from 'vitest';
import { authApi } from '../../services/auth';

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

describe('authApi', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('login calls POST /api/auth/login', async () => {
    const mockResponse = { data: { accessToken: 'token', user: { id: '1', username: 'test' } } };
    vi.mocked(api.post).mockResolvedValue(mockResponse);

    const result = await authApi.login({ username: 'testuser', password: 'pass123' });
    expect(api.post).toHaveBeenCalledWith('/api/auth/login', { username: 'testuser', password: 'pass123' });
    expect(result.data.accessToken).toBe('token');
  });

  it('login with deviceId includes deviceId', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    await authApi.login({ username: 'u', password: 'p', deviceId: 'dev-123' });
    expect(api.post).toHaveBeenCalledWith('/api/auth/login', { username: 'u', password: 'p', deviceId: 'dev-123' });
  });

  it('register calls POST /api/auth/register', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { accessToken: 'tok' } });
    await authApi.register({ username: 'new', password: 'pass', displayName: 'New' });
    expect(api.post).toHaveBeenCalledWith('/api/auth/register', { username: 'new', password: 'pass', displayName: 'New' });
  });

  it('refresh calls POST /api/auth/refresh', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { accessToken: 'newtok', refreshToken: 'newref' } });
    const result = await authApi.refresh('oldrefresh');
    expect(api.post).toHaveBeenCalledWith('/api/auth/refresh', { refreshToken: 'oldrefresh' });
    expect(result.data.accessToken).toBe('newtok');
  });

  it('logout calls POST /api/auth/logout', async () => {
    vi.mocked(api.post).mockResolvedValue({});
    await authApi.logout('session1');
    expect(api.post).toHaveBeenCalledWith('/api/auth/logout', { sessionId: 'session1' });
  });

  it('getMe calls GET /api/auth/me', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { id: '1', username: 'test' } });
    const result = await authApi.getMe();
    expect(api.get).toHaveBeenCalledWith('/api/auth/me');
    expect(result.data.username).toBe('test');
  });

  it('registerDevice calls POST /api/auth/devices/register', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { success: true, deviceId: 'dev-1', trustScore: 80, requiresVerification: false } });
    const result = await authApi.registerDevice({ deviceId: 'dev-1', deviceName: 'PC', osVersion: 'Win10', fingerprint: 'fp' });
    expect(api.post).toHaveBeenCalledWith('/api/auth/devices/register', { deviceId: 'dev-1', deviceName: 'PC', osVersion: 'Win10', fingerprint: 'fp' });
    expect(result.data.success).toBe(true);
  });

  it('getSessions calls GET /api/auth/sessions', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    const result = await authApi.getSessions();
    expect(api.get).toHaveBeenCalledWith('/api/auth/sessions');
    expect(result.data).toEqual([]);
  });

  it('terminateSession calls POST terminate', async () => {
    vi.mocked(api.post).mockResolvedValue({});
    await authApi.terminateSession('session1');
    expect(api.post).toHaveBeenCalledWith('/api/auth/sessions/session1/terminate');
  });

  it('handles API error gracefully', async () => {
    vi.mocked(api.post).mockRejectedValue(new Error('Network Error'));
    await expect(authApi.login({ username: 'u', password: 'p' })).rejects.toThrow('Network Error');
  });
});
