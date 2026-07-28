import { describe, it, expect, beforeEach } from 'vitest';
import { useNotificationStore } from '../../stores/notificationStore';
import type { INotification } from '../../types/global';

function makeNotif(overrides: Partial<INotification> = {}): INotification {
  return {
    id: 'n1',
    type: 'info',
    title: 'Test',
    message: 'Test message',
    timestamp: new Date().toISOString(),
    read: false,
    ...overrides,
  };
}

describe('notificationStore', () => {
  beforeEach(() => {
    useNotificationStore.setState({ notifications: [], unreadCount: 0 });
  });

  it('starts empty', () => {
    const s = useNotificationStore.getState();
    expect(s.notifications).toEqual([]);
    expect(s.unreadCount).toBe(0);
  });

  it('addNotification adds and increments unread', () => {
    useNotificationStore.getState().addNotification(makeNotif());
    const s = useNotificationStore.getState();
    expect(s.notifications).toHaveLength(1);
    expect(s.unreadCount).toBe(1);
  });

  it('addNotification does not increment for read notification', () => {
    useNotificationStore.getState().addNotification(makeNotif({ read: true }));
    expect(useNotificationStore.getState().unreadCount).toBe(0);
  });

  it('markRead marks notification read', () => {
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n1' }));
    useNotificationStore.getState().markRead('n1');
    const s = useNotificationStore.getState();
    expect(s.notifications[0].read).toBe(true);
    expect(s.unreadCount).toBe(0);
  });

  it('markRead does nothing for already read', () => {
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n1', read: true }));
    useNotificationStore.getState().markRead('n1');
    expect(useNotificationStore.getState().unreadCount).toBe(0);
  });

  it('markRead does nothing for unknown id', () => {
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n1' }));
    useNotificationStore.getState().markRead('nonexistent');
    expect(useNotificationStore.getState().unreadCount).toBe(1);
  });

  it('markAllRead marks all as read', () => {
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n1' }));
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n2' }));
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n3' }));
    useNotificationStore.getState().markAllRead();
    const s = useNotificationStore.getState();
    expect(s.notifications.every((n) => n.read)).toBe(true);
    expect(s.unreadCount).toBe(0);
  });

  it('removeNotification removes and decrements unread', () => {
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n1' }));
    useNotificationStore.getState().addNotification(makeNotif({ id: 'n2' }));
    useNotificationStore.getState().removeNotification('n1');
    expect(useNotificationStore.getState().notifications).toHaveLength(1);
    expect(useNotificationStore.getState().unreadCount).toBe(1);
  });

  it('clearAll clears everything', () => {
    useNotificationStore.getState().addNotification(makeNotif());
    useNotificationStore.getState().addNotification(makeNotif());
    useNotificationStore.getState().clearAll();
    const s = useNotificationStore.getState();
    expect(s.notifications).toEqual([]);
    expect(s.unreadCount).toBe(0);
  });
});
