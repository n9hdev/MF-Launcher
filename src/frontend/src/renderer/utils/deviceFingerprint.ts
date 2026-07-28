function getScreenFingerprint(): string {
  const { screen } = window;
  return `${screen.width}x${screen.height}x${screen.colorDepth}`;
}

function getNavigatorFingerprint(): string {
  const nav = navigator;
  const parts = [
    nav.userAgent,
    nav.language,
    nav.platform,
    nav.hardwareConcurrency,
    nav.maxTouchPoints,
    !!nav.webdriver,
  ];
  return parts.join('|');
}

function getCanvasFingerprint(): string {
  const canvas = document.createElement('canvas');
  canvas.width = 200;
  canvas.height = 50;
  const ctx = canvas.getContext('2d');
  if (!ctx) return 'canvas-unavailable';

  ctx.textBaseline = 'top';
  ctx.font = '14px Arial';
  ctx.fillStyle = '#f60';
  ctx.fillRect(125, 1, 62, 20);
  ctx.fillStyle = '#069';
  ctx.fillText('AC-V6', 2, 15);
  return canvas.toDataURL();
}

function hashString(str: string): string {
  let hash = 0;
  for (let i = 0; i < str.length; i++) {
    const char = str.charCodeAt(i);
    hash = ((hash << 5) - hash) + char;
    hash |= 0;
  }
  return Math.abs(hash).toString(16).padStart(8, '0');
}

export function generateDeviceId(): string {
  const fingerprint = [
    getScreenFingerprint(),
    getNavigatorFingerprint(),
    getCanvasFingerprint(),
  ].join('---');
  return `dev-${hashString(fingerprint)}`;
}

export function getDeviceInfo() {
  return {
    deviceId: generateDeviceId(),
    deviceName: navigator.platform || 'Unknown Device',
    osVersion: navigator.userAgent,
    fingerprint: getCanvasFingerprint(),
  };
}
