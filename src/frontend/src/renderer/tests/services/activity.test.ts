import { describe, it, expect, vi, beforeEach } from 'vitest';
import { activityApi } from '../../services/activity';

vi.mock('../../services/api', () => {
  const mockAxios = {
    get: vi.fn(),
    defaults: { baseURL: 'http://localhost:5000' },
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  };
  return { default: mockAxios };
});

import api from '../../services/api';

describe('activityApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('getRecentActivity calls GET /api/activity with count param', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    const result = await activityApi.getRecentActivity();
    expect(api.get).toHaveBeenCalledWith('/api/activity', { params: { count: undefined } });
  });

  it('getRecentActivity with count passes param', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    await activityApi.getRecentActivity(20);
    expect(api.get).toHaveBeenCalledWith('/api/activity', { params: { count: 20 } });
  });

  it('handles error', async () => {
    vi.mocked(api.get).mockRejectedValue(new Error('Failed'));
    await expect(activityApi.getRecentActivity()).rejects.toThrow('Failed');
  });
});
