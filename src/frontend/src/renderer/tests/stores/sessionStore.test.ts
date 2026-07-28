import { describe, it, expect, beforeEach } from 'vitest';
import { useSessionStore } from '../../stores/sessionStore';

describe('sessionStore', () => {
  beforeEach(() => {
    useSessionStore.setState({
      gameRunning: false,
      gameName: null,
      gamePath: null,
      sessionStartTime: null,
      sessionDuration: 0,
      protectionActive: true,
    });
  });

  it('starts with no game running', () => {
    const s = useSessionStore.getState();
    expect(s.gameRunning).toBe(false);
    expect(s.protectionActive).toBe(true);
  });

  it('setGameRunning starts a session', () => {
    useSessionStore.getState().setGameRunning(true, 'MTA SA', 'C:\\games\\mta.exe');
    const s = useSessionStore.getState();
    expect(s.gameRunning).toBe(true);
    expect(s.gameName).toBe('MTA SA');
    expect(s.gamePath).toBe('C:\\games\\mta.exe');
    expect(s.sessionStartTime).toBeTruthy();
    expect(s.sessionDuration).toBe(0);
  });

  it('setGameRunning stops a session when false', () => {
    useSessionStore.getState().setGameRunning(true, 'MTA', 'C:\\mta.exe');
    useSessionStore.getState().setGameRunning(false);
    const s = useSessionStore.getState();
    expect(s.gameRunning).toBe(false);
    expect(s.sessionStartTime).toBeNull();
    expect(s.sessionDuration).toBe(0);
  });

  it('stopGame stops the game', () => {
    useSessionStore.getState().setGameRunning(true, 'MTA', 'C:\\mta.exe');
    useSessionStore.getState().stopGame();
    const s = useSessionStore.getState();
    expect(s.gameRunning).toBe(false);
    expect(s.gameName).toBeNull();
    expect(s.gamePath).toBeNull();
  });

  it('setProtectionActive toggles protection', () => {
    useSessionStore.getState().setProtectionActive(false);
    expect(useSessionStore.getState().protectionActive).toBe(false);
  });

  it('tickDuration increments duration', () => {
    useSessionStore.getState().tickDuration();
    expect(useSessionStore.getState().sessionDuration).toBe(1);
    useSessionStore.getState().tickDuration();
    expect(useSessionStore.getState().sessionDuration).toBe(2);
  });
});
