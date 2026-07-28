import { describe, it, expect, vi, beforeEach } from 'vitest';
import { historyApi } from '../../services/history';

vi.mock('../../services/api', () => {
  const mockAxios = {
    get: vi.fn(),
    defaults: { baseURL: 'http://localhost:5000' },
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  };
  return { default: mockAxios };
});

import api from '../../services/api';

describe('historyApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('getTimeline calls GET /api/history', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    const result = await historyApi.getTimeline();
    expect(api.get).toHaveBeenCalledWith('/api/history', { params: undefined });
  });

  it('getTimeline with params', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    await historyApi.getTimeline({ severity: 'high', limit: 10 });
    expect(api.get).toHaveBeenCalledWith('/api/history', { params: { severity: 'high', limit: 10 } });
  });

  it('getSummary calls GET /api/history/summary', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: {} });
    await historyApi.getSummary();
    expect(api.get).toHaveBeenCalledWith('/api/history/summary');
  });

  it('getStats calls GET /api/history/stats', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: {} });
    await historyApi.getStats();
    expect(api.get).toHaveBeenCalledWith('/api/history/stats');
  });

  it('handles error', async () => {
    vi.mocked(api.get).mockRejectedValue(new Error('Failed'));
    await expect(historyApi.getTimeline()).rejects.toThrow('Failed');
  });
});
