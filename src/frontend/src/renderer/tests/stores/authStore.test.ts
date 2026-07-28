import { describe, it, expect, beforeEach } from 'vitest';
import { useAuthStore } from '../../stores/authStore';

const mockUser = {
  id: '1',
  username: 'testuser',
  displayName: 'Test User',
  role: 'admin' as const,
  trustScore: 75,
  level: 5,
  xp: 500,
  nextLevelXp: 1000,
  status: 'online' as const,
  createdAt: '2025-01-01T00:00:00Z',
  lastLogin: '2025-06-01T00:00:00Z',
  badges: [],
};

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.setState({
      user: null,
      token: null,
      refreshToken: null,
      sessionId: null,
      deviceId: null,
      isAuthenticated: false,
      isLoggingIn: false,
      loginError: null,
    });
    localStorage.clear();
  });

  it('starts unauthenticated', () => {
    const s = useAuthStore.getState();
    expect(s.isAuthenticated).toBe(false);
    expect(s.user).toBeNull();
    expect(s.token).toBeNull();
  });

  it('setAuth sets authenticated state', () => {
    useAuthStore.getState().setAuth(mockUser, 'token123', 'refresh123', 'session1');
    const s = useAuthStore.getState();
    expect(s.isAuthenticated).toBe(true);
    expect(s.user).toEqual(mockUser);
    expect(s.token).toBe('token123');
    expect(s.refreshToken).toBe('refresh123');
    expect(s.sessionId).toBe('session1');
    expect(s.loginError).toBeNull();
  });

  it('setTokens updates tokens', () => {
    useAuthStore.getState().setAuth(mockUser, 'old', 'oldr', 's1');
    useAuthStore.getState().setTokens('newtoken', 'newrefresh');
    const s = useAuthStore.getState();
    expect(s.token).toBe('newtoken');
    expect(s.refreshToken).toBe('newrefresh');
  });

  it('setDeviceId sets device id', () => {
    useAuthStore.getState().setDeviceId('dev-abc');
    expect(useAuthStore.getState().deviceId).toBe('dev-abc');
  });

  it('logout clears auth state', () => {
    useAuthStore.getState().setAuth(mockUser, 'tok', 'ref', 's1');
    useAuthStore.getState().logout();
    const s = useAuthStore.getState();
    expect(s.isAuthenticated).toBe(false);
    expect(s.user).toBeNull();
    expect(s.token).toBeNull();
    expect(s.refreshToken).toBeNull();
    expect(s.sessionId).toBeNull();
  });

  it('setLoggingIn toggles login state', () => {
    useAuthStore.getState().setLoggingIn(true);
    expect(useAuthStore.getState().isLoggingIn).toBe(true);
    useAuthStore.getState().setLoggingIn(false);
    expect(useAuthStore.getState().isLoggingIn).toBe(false);
  });

  it('setLoginError sets error', () => {
    useAuthStore.getState().setLoginError('Invalid credentials');
    expect(useAuthStore.getState().loginError).toBe('Invalid credentials');
    useAuthStore.getState().setLoginError(null);
    expect(useAuthStore.getState().loginError).toBeNull();
  });

  it('updateTrustScore updates user trust score', () => {
    useAuthStore.getState().setAuth(mockUser, 'tok', 'ref', 's1');
    useAuthStore.getState().updateTrustScore(90);
    expect(useAuthStore.getState().user?.trustScore).toBe(90);
  });

  it('updateTrustScore does nothing when no user', () => {
    useAuthStore.getState().updateTrustScore(90);
    expect(useAuthStore.getState().user).toBeNull();
  });

  it('updateUser partially updates user', () => {
    useAuthStore.getState().setAuth(mockUser, 'tok', 'ref', 's1');
    useAuthStore.getState().updateUser({ displayName: 'Updated Name' });
    expect(useAuthStore.getState().user?.displayName).toBe('Updated Name');
    expect(useAuthStore.getState().user?.username).toBe('testuser');
  });

  it('updateUser does nothing when no user', () => {
    useAuthStore.getState().updateUser({ displayName: 'X' });
    expect(useAuthStore.getState().user).toBeNull();
  });
});
