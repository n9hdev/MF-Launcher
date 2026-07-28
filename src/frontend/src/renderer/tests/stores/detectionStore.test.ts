import { describe, it, expect, beforeEach } from 'vitest';
import { useDetectionStore } from '../../stores/detectionStore';
import type { IDetectionEvent } from '../../types/global';

function makeEvent(overrides: Partial<IDetectionEvent> = {}): IDetectionEvent {
  return {
    id: 'e1',
    type: 'Memory Hack',
    severity: 'high',
    timestamp: new Date().toISOString(),
    description: 'Suspicious memory pattern',
    confidence: 0.85,
    resolved: false,
    ...overrides,
  };
}

describe('detectionStore', () => {
  beforeEach(() => {
    useDetectionStore.setState({
      events: [],
      scanRunning: false,
      lastScanTime: null,
    });
  });

  it('starts with empty events', () => {
    const s = useDetectionStore.getState();
    expect(s.events).toEqual([]);
    expect(s.scanRunning).toBe(false);
  });

  it('addEvent prepends event and caps at 500', () => {
    const store = useDetectionStore.getState();
    store.addEvent(makeEvent({ id: 'e1' }));
    expect(useDetectionStore.getState().events).toHaveLength(1);

    const many = Array.from({ length: 600 }, (_, i) => makeEvent({ id: `e${i}` }));
    for (const e of many) {
      useDetectionStore.getState().addEvent(e);
    }
    expect(useDetectionStore.getState().events.length).toBeLessThanOrEqual(500);
  });

  it('updateStatus partially updates status', () => {
    useDetectionStore.getState().updateStatus({ memoryScanner: 'inactive' });
    const s = useDetectionStore.getState();
    expect(s.status.memoryScanner).toBe('inactive');
    expect(s.status.processAnalyzer).toBe('active');
  });

  it('updateHealth partially updates health', () => {
    useDetectionStore.getState().updateHealth({ cpuUsage: 90 });
    expect(useDetectionStore.getState().health.cpuUsage).toBe(90);
  });

  it('setScanRunning sets scan state', () => {
    useDetectionStore.getState().setScanRunning(true);
    expect(useDetectionStore.getState().scanRunning).toBe(true);
  });

  it('setLastScanTime sets scan time', () => {
    useDetectionStore.getState().setLastScanTime('2025-01-01T00:00:00Z');
    expect(useDetectionStore.getState().lastScanTime).toBe('2025-01-01T00:00:00Z');
  });

  it('clearEvents empties events', () => {
    useDetectionStore.getState().addEvent(makeEvent());
    useDetectionStore.getState().clearEvents();
    expect(useDetectionStore.getState().events).toEqual([]);
  });

  it('resolveEvent marks event resolved', () => {
    useDetectionStore.getState().addEvent(makeEvent({ id: 'e1' }));
    useDetectionStore.getState().resolveEvent('e1', 'admin1');
    const ev = useDetectionStore.getState().events[0];
    expect(ev.resolved).toBe(true);
    expect(ev.resolvedBy).toBe('admin1');
    expect(ev.resolvedAt).toBeDefined();
  });

  it('resolveEvent does nothing for unknown id', () => {
    useDetectionStore.getState().addEvent(makeEvent({ id: 'e1' }));
    useDetectionStore.getState().resolveEvent('nonexistent');
    expect(useDetectionStore.getState().events[0].resolved).toBe(false);
  });
});
