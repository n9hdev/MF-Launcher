import { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useLocation, useNavigate } from 'react-router-dom';
import {
  Search, Bell, Users, Settings, LogOut, Shield,
  Sun, Moon, ChevronDown, Maximize2, Minimize2, X,
  MessageSquare, AlertTriangle, Info, CheckCircle, Wifi, WifiOff,
} from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { useUIStore } from '../../stores/uiStore';
import { useNotificationStore } from '../../stores/notificationStore';
import { usePermissionStore } from '../../stores/permissionStore';
import { useTheme } from '../../theme/ThemeProvider';
import { useSessionStore } from '../../stores/sessionStore';
import { useDetectionStore } from '../../stores/detectionStore';
import { authApi } from '../../services/auth';
import { disconnectSignalR } from '../../services/signalr';
import { healthApi, type IHealthStatus } from '../../services/health';
import { moderatorApi } from '../../services/moderator';

const breadcrumbMap: Record<string, string> = {
  dashboard: 'Dashboard',
  protection: 'Protection Status',
  launch: 'Game Launch',
  forbidden: 'Access Denied',
  history: 'Detection History',
  reports: 'Reports',
  players: 'Player Search',
  alerts: 'Active Alerts',
  chat: 'Mod Chat',
  bans: 'Ban Center',
  analytics: 'Analytics',
  appeals: 'Appeals',
  whitelist: 'Whitelist Management',
  command: 'Command Center',
  telemetry: 'Telemetry',
  detection: 'Detection Center',
  rules: 'Rule Engine',
  infrastructure: 'Infrastructure',
  audit: 'Audit Log',
  settings: 'Settings',
};

const trustBadgeConfig: Record<string, { label: string; color: string; bg: string }> = {
  trusted: { label: 'Trusted', color: 'text-emerald-400', bg: 'bg-emerald-500/10 border-emerald-500/20' },
  pending: { label: 'Pending', color: 'text-amber-400', bg: 'bg-amber-500/10 border-amber-500/20' },
  restricted: { label: 'Restricted', color: 'text-rose-400', bg: 'bg-rose-500/10 border-rose-500/20' },
};

export function FloatingTopBar() {
  const location = useLocation();
  const { user, logout, sessionId, trustStatus } = useAuthStore();
  const { toggleSearch, toggleCommandPalette, sidebarCollapsed, setSidebarCollapsed } = useUIStore();
  const { notifications, unreadCount, markAllRead } = useNotificationStore();
  const { mode, toggleMode } = useTheme();
  const { gameRunning } = useSessionStore();
  const { health } = useDetectionStore();

  const navigate = useNavigate();
  const [userMenuOpen, setUserMenuOpen] = useState(false);
  const [notifOpen, setNotifOpen] = useState(false);
  const [serverStatus, setServerStatus] = useState<IHealthStatus | null>(null);
  const [activePlayers, setActivePlayers] = useState<number | null>(null);
  const userRef = useRef<HTMLDivElement>(null);
  const notifRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const fetch = async () => {
      try {
        const { data } = await healthApi.getStatus();
        setServerStatus(data);
      } catch { setServerStatus(null); }
      try {
        const { data } = await moderatorApi.getStats();
        setActivePlayers(data.activePlayers);
      } catch { /* ignore */ }
    };
    fetch();
    const interval = setInterval(fetch, 30000);
    return () => clearInterval(interval);
  }, []);

  const pathSegments = location.pathname.split('/').filter(Boolean);
  const currentPage = pathSegments[pathSegments.length - 1] || 'dashboard';
  const pageTitle = breadcrumbMap[currentPage] || currentPage.charAt(0).toUpperCase() + currentPage.slice(1);

  useEffect(() => {
    const handler = (e: MouseEvent) => {
      if (userRef.current && !userRef.current.contains(e.target as Node)) setUserMenuOpen(false);
      if (notifRef.current && !notifRef.current.contains(e.target as Node)) setNotifOpen(false);
    };
    document.addEventListener('mousedown', handler);
    return () => document.removeEventListener('mousedown', handler);
  }, []);

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === 'k') { e.preventDefault(); toggleCommandPalette(); }
      if ((e.ctrlKey || e.metaKey) && e.key === 'f') { e.preventDefault(); toggleSearch(); }
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [toggleCommandPalette, toggleSearch]);

  return (
    <header
      className="h-14 flex items-center justify-between px-5 border-b border-white/5 z-40"
      style={{ background: 'rgba(15, 23, 42, 0.75)', backdropFilter: 'blur(20px) saturate(1.4)' }}
    >
      <div className="flex items-center gap-4">
        <button
          onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
          className="text-white/30 hover:text-white/60 transition-colors"
        >
          {sidebarCollapsed ? <Maximize2 size={16} /> : <Minimize2 size={16} />}
        </button>

        <div className="flex items-center gap-2 text-sm">
          <span className="text-white/30">/</span>
          {pathSegments.map((seg, i) => (
            <span key={seg} className="flex items-center gap-2">
              <span className={i === pathSegments.length - 1 ? 'text-white/80 font-medium' : 'text-white/30'}>{breadcrumbMap[seg] || seg}</span>
              {i < pathSegments.length - 1 && <span className="text-white/20">/</span>}
            </span>
          ))}
        </div>
      </div>

      <div className="flex items-center gap-2">
        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          onClick={toggleSearch}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg glass glass-hover text-xs text-white/40"
        >
          <Search size={14} />
          <span className="hidden sm:inline">Search</span>
          <kbd className="text-[10px] px-1 py-0.5 rounded bg-white/5 text-white/20 font-mono">Ctrl+F</kbd>
        </motion.button>

        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          onClick={toggleCommandPalette}
          className="flex items-center gap-2 px-3 py-1.5 rounded-lg glass glass-hover text-xs text-white/40"
        >
          <span className="hidden sm:inline">Commands</span>
          <kbd className="text-[10px] px-1 py-0.5 rounded bg-white/5 text-white/20 font-mono">Ctrl+K</kbd>
        </motion.button>

        {serverStatus && (
          <div className={`flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg border ${
            serverStatus.healthy ? 'bg-emerald-500/10 border-emerald-500/20' : 'bg-rose-500/10 border-rose-500/20'
          }`}>
            {serverStatus.healthy ? <Wifi size={11} className="text-emerald-400" /> : <WifiOff size={11} className="text-rose-400" />}
            <span className={`text-[10px] font-medium ${serverStatus.healthy ? 'text-emerald-400' : 'text-rose-400'}`}>
              {serverStatus.healthy ? 'Online' : 'Degraded'}
            </span>
          </div>
        )}

        {activePlayers !== null && (
          <div className="flex items-center gap-1.5 px-2.5 py-1.5 rounded-lg bg-white/5 border border-white/10">
            <Users size={11} className="text-white/40" />
            <span className="text-[10px] text-white/50 font-mono">{activePlayers.toLocaleString()}</span>
          </div>
        )}

        {gameRunning && (
          <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-500/10 border border-emerald-500/20">
            <div className="w-1.5 h-1.5 rounded-full bg-emerald-500 animate-pulse" />
            <span className="text-[10px] text-emerald-400 font-medium">INGAME</span>
          </div>
        )}

        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          onClick={toggleMode}
          className="w-8 h-8 flex items-center justify-center rounded-lg glass glass-hover text-white/40"
        >
          {mode === 'dark' ? <Sun size={14} /> : <Moon size={14} />}
        </motion.button>

        <div className="relative" ref={notifRef}>
          <motion.button
            whileHover={{ scale: 1.05 }}
            whileTap={{ scale: 0.95 }}
            onClick={() => setNotifOpen(!notifOpen)}
            className="w-8 h-8 flex items-center justify-center rounded-lg glass glass-hover text-white/40 relative"
          >
            <Bell size={14} />
            {unreadCount > 0 && (
              <span className="absolute -top-0.5 -right-0.5 w-4 h-4 rounded-full bg-rose-500 text-[9px] font-bold text-white flex items-center justify-center">
                {unreadCount > 9 ? '9+' : unreadCount}
              </span>
            )}
          </motion.button>

          <AnimatePresence>
            {notifOpen && (
              <motion.div
                initial={{ opacity: 0, y: 8, scale: 0.96 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 8, scale: 0.96 }}
                transition={{ duration: 0.15 }}
                className="absolute right-0 top-full mt-2 w-80 rounded-xl overflow-hidden z-50"
                style={{ background: 'rgba(15, 23, 42, 0.95)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
              >
                <div className="flex items-center justify-between px-4 py-3 border-b border-white/5">
                  <span className="text-sm font-semibold text-white/80">Notifications</span>
                  <button onClick={markAllRead} className="text-[10px] text-primary-400 hover:text-primary-300">Mark all read</button>
                </div>
                <div className="max-h-80 overflow-y-auto">
                  {notifications.slice(0, 8).map((n) => (
                    <div key={n.id} className={`flex items-start gap-3 px-4 py-3 border-b border-white/5 last:border-0 transition-colors ${n.read ? 'opacity-50' : 'bg-primary-500/5'}`}>
                      <div className={`mt-0.5 ${n.type === 'success' ? 'text-emerald-400' : n.type === 'warning' ? 'text-amber-400' : n.type === 'error' ? 'text-rose-400' : n.type === 'achievement' ? 'text-violet-400' : 'text-primary-400'}`}>
                        {n.type === 'success' ? <CheckCircle size={14} /> : n.type === 'warning' ? <AlertTriangle size={14} /> : n.type === 'error' ? <X size={14} /> : n.type === 'achievement' ? <Shield size={14} /> : <Info size={14} />}
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-xs font-medium text-white/80 truncate">{n.title}</p>
                        <p className="text-[11px] text-white/40 truncate">{n.message}</p>
                      </div>
                      <span className="text-[10px] text-white/20 flex-shrink-0">{n.timestamp}</span>
                    </div>
                  ))}
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>

        {trustStatus && (
          <div className={`flex items-center gap-1.5 px-2.5 py-1 rounded-lg border ${trustBadgeConfig[trustStatus]?.bg || ''}`}>
            <div className={`w-1.5 h-1.5 rounded-full ${trustBadgeConfig[trustStatus]?.color || 'text-white/30'} ${trustStatus === 'trusted' ? 'animate-pulse' : ''}`}
              style={{ backgroundColor: trustStatus === 'trusted' ? '#34d399' : trustStatus === 'restricted' ? '#fb7185' : '#fbbf24' }}
            />
            <span className={`text-[10px] font-medium ${trustBadgeConfig[trustStatus]?.color || 'text-white/30'}`}>
              {trustBadgeConfig[trustStatus]?.label || trustStatus}
            </span>
          </div>
        )}

        <div className="relative" ref={userRef}>
          <motion.button
            whileHover={{ scale: 1.02 }}
            whileTap={{ scale: 0.98 }}
            onClick={() => setUserMenuOpen(!userMenuOpen)}
            className="flex items-center gap-2.5 px-3 py-1.5 rounded-lg glass glass-hover"
          >
            <div className="w-7 h-7 rounded-full bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center text-xs font-bold text-white">
              {user?.displayName?.charAt(0) || user?.username?.charAt(0) || 'U'}
            </div>
            <div className="hidden sm:block text-left">
              <p className="text-xs font-medium text-white/80 leading-tight">{user?.displayName || user?.username}</p>
              <p className="text-[10px] text-white/30 leading-tight capitalize">{user?.role}</p>
            </div>
            <ChevronDown size={12} className="text-white/30" />
          </motion.button>

          <AnimatePresence>
            {userMenuOpen && (
              <motion.div
                initial={{ opacity: 0, y: 8, scale: 0.96 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 8, scale: 0.96 }}
                transition={{ duration: 0.15 }}
                className="absolute right-0 top-full mt-2 w-56 rounded-xl overflow-hidden z-50"
                style={{ background: 'rgba(15, 23, 42, 0.95)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
              >
                <div className="px-4 py-3 border-b border-white/5">
                  <p className="text-sm font-medium text-white/80">{user?.displayName || user?.username}</p>
                  <p className="text-[11px] text-white/30">{user?.email || `${user?.username}@mafia.city`}</p>
                </div>
                <div className="py-1">
                  {[
                    { label: 'Profile', icon: <Users size={14} />, onClick: () => { navigate('/dashboard'); setUserMenuOpen(false); } },
                    { label: 'Settings', icon: <Settings size={14} />, onClick: () => { navigate('/settings'); setUserMenuOpen(false); } },
                  ].map((item) => (
                    <button
                      key={item.label}
                      onClick={item.onClick}
                      className="w-full flex items-center gap-3 px-4 py-2 text-xs text-white/50 hover:text-white/80 hover:bg-white/5 transition-colors"
                    >
                      {item.icon}
                      {item.label}
                    </button>
                  ))}
                </div>
                <div className="border-t border-white/5 py-1">
                    <button
                      onClick={async () => {
                        try {
                          if (sessionId) await authApi.logout(sessionId);
                        } catch { /* ignore */ }
                        await disconnectSignalR();
                        usePermissionStore.getState().reset();
                        logout();
                      }}
                      className="w-full flex items-center gap-3 px-4 py-2 text-xs text-rose-400/70 hover:text-rose-400 hover:bg-white/5 transition-colors"
                    >
                      <LogOut size={14} />
                      Sign Out
                    </button>
                </div>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </header>
  );
}
