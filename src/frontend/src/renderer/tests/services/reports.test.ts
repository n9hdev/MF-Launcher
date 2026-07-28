import { describe, it, expect, vi, beforeEach } from 'vitest';
import { reportApi } from '../../services/reports';

vi.mock('../../services/api', () => {
  const mockAxios = {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    defaults: { baseURL: 'http://localhost:5000' },
    interceptors: { request: { use: vi.fn() }, response: { use: vi.fn() } },
  };
  return { default: mockAxios };
});

import api from '../../services/api';

describe('reportApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('getMyReports calls GET /api/reports/my', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    const result = await reportApi.getMyReports();
    expect(api.get).toHaveBeenCalledWith('/api/reports/my');
    expect(result.data).toEqual([]);
  });

  it('submitReport calls POST /api/reports', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { id: 'r1' } });
    const payload = { playerName: 'Player1', reason: 'Cheating', description: 'Saw aimbot' };
    const result = await reportApi.submitReport(payload);
    expect(api.post).toHaveBeenCalledWith('/api/reports', payload);
    expect(result.data.id).toBe('r1');
  });

  it('getReport calls GET /api/reports/:id', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { id: 'r1' } });
    const result = await reportApi.getReport('r1');
    expect(api.get).toHaveBeenCalledWith('/api/reports/r1');
    expect(result.data.id).toBe('r1');
  });

  it('handles API error', async () => {
    vi.mocked(api.get).mockRejectedValue(new Error('Not found'));
    await expect(reportApi.getReport('bad')).rejects.toThrow('Not found');
  });
});
