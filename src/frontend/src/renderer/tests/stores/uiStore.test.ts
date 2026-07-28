import { describe, it, expect, beforeEach } from 'vitest';
import { useUIStore } from '../../stores/uiStore';

describe('uiStore', () => {
  beforeEach(() => {
    useUIStore.setState({
      sidebarOpen: true,
      sidebarCollapsed: false,
      infoDrawerOpen: false,
      infoDrawerContent: null,
      commandPaletteOpen: false,
      searchOpen: false,
      contextMenu: null,
      toasts: [],
      activeModal: null,
    });
  });

  it('starts with sidebar open', () => {
    expect(useUIStore.getState().sidebarOpen).toBe(true);
  });

  it('toggleSidebar toggles sidebar', () => {
    useUIStore.getState().toggleSidebar();
    expect(useUIStore.getState().sidebarOpen).toBe(false);
    useUIStore.getState().toggleSidebar();
    expect(useUIStore.getState().sidebarOpen).toBe(true);
  });

  it('setSidebarCollapsed sets collapsed', () => {
    useUIStore.getState().setSidebarCollapsed(true);
    expect(useUIStore.getState().sidebarCollapsed).toBe(true);
  });

  it('toggleInfoDrawer toggles drawer', () => {
    useUIStore.getState().toggleInfoDrawer();
    expect(useUIStore.getState().infoDrawerOpen).toBe(true);
  });

  it('setInfoDrawerContent opens drawer with content', () => {
    useUIStore.getState().setInfoDrawerContent('content');
    expect(useUIStore.getState().infoDrawerOpen).toBe(true);
    expect(useUIStore.getState().infoDrawerContent).toBe('content');
  });

  it('setInfoDrawerContent null closes drawer', () => {
    useUIStore.getState().setInfoDrawerContent('content');
    useUIStore.getState().setInfoDrawerContent(null);
    expect(useUIStore.getState().infoDrawerOpen).toBe(false);
    expect(useUIStore.getState().infoDrawerContent).toBeNull();
  });

  it('toggleCommandPalette toggles', () => {
    useUIStore.getState().toggleCommandPalette();
    expect(useUIStore.getState().commandPaletteOpen).toBe(true);
  });

  it('toggleSearch toggles', () => {
    useUIStore.getState().toggleSearch();
    expect(useUIStore.getState().searchOpen).toBe(true);
  });

  it('open/closeContextMenu works', () => {
    const items = [{ label: 'Action', action: () => {} }];
    useUIStore.getState().openContextMenu(100, 200, items);
    const menu = useUIStore.getState().contextMenu;
    expect(menu).not.toBeNull();
    expect(menu!.x).toBe(100);
    expect(menu!.y).toBe(200);
    expect(menu!.items).toHaveLength(1);

    useUIStore.getState().closeContextMenu();
    expect(useUIStore.getState().contextMenu).toBeNull();
  });

  it('addToast adds and removes after duration', () => {
    vi.useFakeTimers();
    useUIStore.getState().addToast({ type: 'info', title: 'Hello', duration: 1000 });
    expect(useUIStore.getState().toasts).toHaveLength(1);

    vi.advanceTimersByTime(1000);
    expect(useUIStore.getState().toasts).toHaveLength(0);
    vi.useRealTimers();
  });

  it('removeToast removes by id', () => {
    useUIStore.getState().addToast({ type: 'info', title: 'T1' });
    const id = useUIStore.getState().toasts[0].id;
    useUIStore.getState().removeToast(id);
    expect(useUIStore.getState().toasts).toHaveLength(0);
  });

  it('setActiveModal sets modal', () => {
    useUIStore.getState().setActiveModal('ban-modal');
    expect(useUIStore.getState().activeModal).toBe('ban-modal');
    useUIStore.getState().setActiveModal(null);
    expect(useUIStore.getState().activeModal).toBeNull();
  });
});
