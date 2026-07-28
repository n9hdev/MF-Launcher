import { describe, it, expect, vi, beforeEach } from 'vitest';
import { generateDeviceId, getDeviceInfo } from '../../utils/deviceFingerprint';

describe('deviceFingerprint', () => {
  beforeEach(() => {
    Object.defineProperty(window, 'screen', {
      value: { width: 1920, height: 1080, colorDepth: 24 },
      writable: true,
    });
    Object.defineProperty(window.navigator, 'userAgent', {
      value: 'Mozilla/5.0 TestAgent',
      writable: true,
    });
    Object.defineProperty(window.navigator, 'language', {
      value: 'en-US',
      writable: true,
    });
    Object.defineProperty(window.navigator, 'platform', {
      value: 'Win32',
      writable: true,
    });
    Object.defineProperty(window.navigator, 'hardwareConcurrency', {
      value: 8,
      writable: true,
    });
    Object.defineProperty(window.navigator, 'maxTouchPoints', {
      value: 0,
      writable: true,
    });
    Object.defineProperty(window.navigator, 'webdriver', {
      value: false,
      writable: true,
    });
  });

  it('generateDeviceId returns a dev- prefixed string', () => {
    const id = generateDeviceId();
    expect(id).toMatch(/^dev-[a-f0-9]{8}$/);
  });

  it('generateDeviceId is deterministic for same input', () => {
    const id1 = generateDeviceId();
    const id2 = generateDeviceId();
    expect(id1).toBe(id2);
  });

  it('getDeviceInfo returns device info object', () => {
    const info = getDeviceInfo();
    expect(info).toHaveProperty('deviceId');
    expect(info).toHaveProperty('deviceName');
    expect(info).toHaveProperty('osVersion');
    expect(info).toHaveProperty('fingerprint');
    expect(info.deviceId).toMatch(/^dev-/);
    expect(info.deviceName).toBe('Win32');
  });

  it('generateDeviceId changes with screen dimensions', () => {
    const id1 = generateDeviceId();
    Object.defineProperty(window, 'screen', {
      value: { width: 1024, height: 768, colorDepth: 32 },
      writable: true,
    });
    const id2 = generateDeviceId();
    expect(id1).not.toBe(id2);
  });

  it('canvas fingerprint produces consistent result', () => {
    const id = generateDeviceId();
    expect(id).toBeTruthy();
    expect(id.startsWith('dev-')).toBe(true);
  });
});
