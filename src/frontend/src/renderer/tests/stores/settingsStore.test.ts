import { describe, it, expect, beforeEach } from 'vitest';
import { useSettingsStore } from '../../stores/settingsStore';

describe('settingsStore', () => {
  beforeEach(() => {
    useSettingsStore.setState({
      notifications: true,
      detectionAlerts: true,
      achievementNotifications: true,
      soundEffects: true,
      minimizeToTray: true,
      startOnBoot: true,
      autoScan: true,
      scanInterval: 30,
      language: 'en',
      reducedMotion: false,
      compactMode: false,
      showFps: false,
      kernelLevelProtection: true,
      submitTelemetry: true,
      automaticUpdates: true,
    });
  });

  it('has default settings', () => {
    const s = useSettingsStore.getState();
    expect(s.language).toBe('en');
    expect(s.scanInterval).toBe(30);
    expect(s.notifications).toBe(true);
  });

  it('setNotifications toggles', () => {
    useSettingsStore.getState().setNotifications(false);
    expect(useSettingsStore.getState().notifications).toBe(false);
  });

  it('setDetectionAlerts toggles', () => {
    useSettingsStore.getState().setDetectionAlerts(false);
    expect(useSettingsStore.getState().detectionAlerts).toBe(false);
  });

  it('setScanInterval changes interval', () => {
    useSettingsStore.getState().setScanInterval(60);
    expect(useSettingsStore.getState().scanInterval).toBe(60);
  });

  it('setLanguage changes language', () => {
    useSettingsStore.getState().setLanguage('fr');
    expect(useSettingsStore.getState().language).toBe('fr');
  });

  it('setReducedMotion toggles', () => {
    useSettingsStore.getState().setReducedMotion(true);
    expect(useSettingsStore.getState().reducedMotion).toBe(true);
  });

  it('setCompactMode toggles', () => {
    useSettingsStore.getState().setCompactMode(true);
    expect(useSettingsStore.getState().compactMode).toBe(true);
  });

  it('setShowFps toggles', () => {
    useSettingsStore.getState().setShowFps(true);
    expect(useSettingsStore.getState().showFps).toBe(true);
  });

  it('setKernelLevelProtection toggles', () => {
    useSettingsStore.getState().setKernelLevelProtection(false);
    expect(useSettingsStore.getState().kernelLevelProtection).toBe(false);
  });

  it('setSubmitTelemetry toggles', () => {
    useSettingsStore.getState().setSubmitTelemetry(false);
    expect(useSettingsStore.getState().submitTelemetry).toBe(false);
  });

  it('setAutomaticUpdates toggles', () => {
    useSettingsStore.getState().setAutomaticUpdates(false);
    expect(useSettingsStore.getState().automaticUpdates).toBe(false);
  });
});
