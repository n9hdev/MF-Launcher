import { describe, it, expect, vi, beforeEach } from 'vitest';
import { detectionApi } from '../../services/detection';

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

describe('detectionApi', () => {
  beforeEach(() => vi.clearAllMocks());

  it('runScan calls POST /api/detection/scan', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: { events: [] } });
    const result = await detectionApi.runScan();
    expect(api.post).toHaveBeenCalledWith('/api/detection/scan');
    expect(result.data.events).toEqual([]);
  });

  it('getEvents calls GET /api/detection/events', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    const result = await detectionApi.getEvents();
    expect(api.get).toHaveBeenCalledWith('/api/detection/events', { params: undefined });
  });

  it('getEvents with params passes params', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: [] });
    await detectionApi.getEvents({ severity: 'high', limit: 10 });
    expect(api.get).toHaveBeenCalledWith('/api/detection/events', { params: { severity: 'high', limit: 10 } });
  });

  it('getEventById calls GET /api/detection/events/:id', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: { id: 'e1' } });
    const result = await detectionApi.getEventById('e1');
    expect(api.get).toHaveBeenCalledWith('/api/detection/events/e1');
    expect(result.data.id).toBe('e1');
  });

  it('getStatus calls GET /api/detection/status', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: {} });
    await detectionApi.getStatus();
    expect(api.get).toHaveBeenCalledWith('/api/detection/status');
  });

  it('toggleDetector calls POST /api/detection/detectors/:name/toggle', async () => {
    vi.mocked(api.post).mockResolvedValue({ data: {} });
    await detectionApi.toggleDetector('Memory Scanner', true);
    expect(api.post).toHaveBeenCalledWith('/api/detection/detectors/Memory Scanner/toggle', { enabled: true });
  });

  it('getHealth calls GET /api/detection/health', async () => {
    vi.mocked(api.get).mockResolvedValue({ data: {} });
    await detectionApi.getHealth();
    expect(api.get).toHaveBeenCalledWith('/api/detection/health');
  });

  it('handles API error', async () => {
    vi.mocked(api.post).mockRejectedValue(new Error('Scan failed'));
    await expect(detectionApi.runScan()).rejects.toThrow('Scan failed');
  });
});
