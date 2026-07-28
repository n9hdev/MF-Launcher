import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { NavLink, useLocation } from 'react-router-dom';
import {
  Shield, LayoutDashboard, Activity, Clock, Flag, Users, Bell,
  MessageSquare, BarChart3, ShieldAlert, ScrollText, Terminal,
  Settings, ChevronLeft, ChevronRight, Search, Gamepad2,
  Siren, Microscope, Network, FileSearch, Server, UserCheck,
  BookOpen, Gavel, Monitor,
} from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import type { UserRole } from '../../types/global';
import { useUIStore } from '../../stores/uiStore';
import { useDetectionStore } from '../../stores/detectionStore';
import { useNotificationStore } from '../../stores/notificationStore';
import { modchatApi } from '../../services/modchat';
import type { IOnlineModerator } from '../../services/modchat';

interface INavSection {
  title: string;
  items: INavItem[];
}

interface INavItem {
  label: string;
  icon: React.ReactNode;
  path: string;
  badge?: number;
  roles: UserRole[];
}

const baseNavSections: INavSection[] = [
  {
    title: 'Overview',
    items: [
      { label: 'Dashboard', icon: <LayoutDashboard size={18} />, path: '/dashboard', roles: ['player'] },
      { label: 'Dashboard', icon: <LayoutDashboard size={18} />, path: '/moderator/dashboard', roles: ['moderator'] },
      { label: 'Dashboard', icon: <LayoutDashboard size={18} />, path: '/admin/dashboard', roles: ['admin', 'superadmin'] },
    ],
  },
  {
    title: 'Player',
    items: [
      { label: 'Protection', icon: <Shield size={18} />, path: '/player/protection', roles: ['player'] },
      { label: 'Game Launch', icon: <Gamepad2 size={18} />, path: '/player/launch', roles: ['player'] },
      { label: 'History', icon: <Clock size={18} />, path: '/player/history', roles: ['player'] },
      { label: 'Reports', icon: <Flag size={18} />, path: '/player/reports', roles: ['player'] },
    ],
  },
  {
    title: 'Moderation',
    items: [
      { label: 'Reports Queue', icon: <Flag size={18} />, path: '/moderator/reports', roles: ['moderator', 'admin', 'superadmin'] },
      { label: 'Player Search', icon: <Search size={18} />, path: '/moderator/players', roles: ['moderator', 'admin', 'superadmin'] },
      { label: 'Active Alerts', icon: <Bell size={18} />, path: '/moderator/alerts', roles: ['moderator', 'admin', 'superadmin'] },
      { label: 'Mod Chat', icon: <MessageSquare size={18} />, path: '/moderator/chat', roles: ['moderator', 'admin', 'superadmin'] },
      { label: 'Flagged Players', icon: <Siren size={18} />, path: '/moderator/flagged', roles: ['moderator', 'admin', 'superadmin'] },
    ],
  },
  {
    title: 'Administration',
    items: [
      { label: 'Ban Center', icon: <Gavel size={18} />, path: '/admin/bans', roles: ['admin', 'superadmin'] },
      { label: 'Analytics', icon: <BarChart3 size={18} />, path: '/admin/analytics', roles: ['admin', 'superadmin'] },
      { label: 'Appeals', icon: <ScrollText size={18} />, path: '/admin/appeals', roles: ['admin', 'superadmin'] },
      { label: 'Whitelist', icon: <UserCheck size={18} />, path: '/admin/whitelist', roles: ['admin', 'superadmin'] },
      { label: 'Live Player View', icon: <Monitor size={18} />, path: '/admin/live-view', roles: ['admin', 'superadmin'] },
    ],
  },
  {
    title: 'SuperAdmin',
    items: [
      { label: 'Command Center', icon: <Terminal size={18} />, path: '/superadmin/command', roles: ['superadmin'] },
      { label: 'Telemetry', icon: <Activity size={18} />, path: '/superadmin/telemetry', roles: ['superadmin'] },
      { label: 'Detection Center', icon: <Microscope size={18} />, path: '/superadmin/detection', roles: ['superadmin'] },
      { label: 'Rule Engine', icon: <BookOpen size={18} />, path: '/superadmin/rules', roles: ['superadmin'] },
      { label: 'Infrastructure', icon: <Server size={18} />, path: '/superadmin/infrastructure', roles: ['superadmin'] },
      { label: 'Audit Log', icon: <FileSearch size={18} />, path: '/superadmin/audit', roles: ['superadmin'] },
    ],
  },
  {
    title: 'System',
    items: [
      { label: 'Settings', icon: <Settings size={18} />, path: '/settings', roles: ['player', 'moderator', 'admin', 'superadmin'] },
    ],
  },
];

const sidebarVariants = {
  expanded: { width: 260, transition: { duration: 0.3, ease: [0.4, 0, 0.2, 1] } },
  collapsed: { width: 68, transition: { duration: 0.3, ease: [0.4, 0, 0.2, 1] } },
};

const itemVariants = {
  expanded: { opacity: 1, x: 0, transition: { duration: 0.2 } },
  collapsed: { opacity: 0, x: -10, transition: { duration: 0.15 } },
};

export function AnimatedSidebar() {
  const { user, isBanned, banInfo } = useAuthStore();
  const { sidebarCollapsed, setSidebarCollapsed } = useUIStore();
  const { events } = useDetectionStore();
  const { unreadCount } = useNotificationStore();
  const location = useLocation();
  const [hoveredSection, setHoveredSection] = useState<string | null>(null);
  const [onlineMods, setOnlineMods] = useState<IOnlineModerator[]>([]);
  const role = user?.role || 'player';

  useEffect(() => {
    if (role === 'player') return;
    const fetch = () => modchatApi.getOnline().then(({ data }) => setOnlineMods(data)).catch(() => {});
    fetch();
    const interval = setInterval(fetch, 15000);
    return () => clearInterval(interval);
  }, [role]);

  const alertCount = events.filter((e) => e.severity === 'high' || e.severity === 'critical').length;
  const reportsCount = events.length;

  const navSections = baseNavSections.map((section) => ({
    ...section,
    items: section.items.map((item) => ({
      ...item,
      badge:
        item.path === '/moderator/reports' && reportsCount > 0 ? reportsCount :
        item.path === '/moderator/alerts' && alertCount > 0 ? alertCount :
        undefined,
    })),
  }));

  const filteredSections = navSections
    .map((section) => ({
      ...section,
      items: section.items.filter((item) => item.roles.includes(role)),
    }))
    .filter((section) => section.items.length > 0);

  const bannedSections: INavSection[] = [
    {
      title: 'Account Status',
      items: [
        { label: 'Banned', icon: <ShieldAlert size={18} />, path: '/banned', roles: ['player'] },
        { label: 'My Appeal', icon: <MessageSquare size={18} />, path: '/player/appeal', roles: ['player'] },
      ],
    },
  ];

  const displaySections = isBanned ? bannedSections : filteredSections;

  return (
    <motion.aside
      variants={sidebarVariants}
      animate={sidebarCollapsed ? 'collapsed' : 'expanded'}
      className="h-full flex flex-col relative border-r border-white/5 overflow-hidden"
      style={{ background: 'rgba(15, 23, 42, 0.85)', backdropFilter: 'blur(24px) saturate(1.4)' }}
    >
      <div className="flex items-center h-14 px-4 border-b border-white/5">
        <motion.div
          animate={{ rotate: sidebarCollapsed ? 180 : 0 }}
          transition={{ duration: 0.3 }}
          className="flex items-center gap-3 flex-1 overflow-hidden"
        >
          <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center flex-shrink-0 glow-primary">
            <Shield size={16} className="text-white" />
          </div>
          <AnimatePresence>
            {!sidebarCollapsed && (
              <motion.div
                initial={{ opacity: 0, width: 0 }}
                animate={{ opacity: 1, width: 'auto' }}
                exit={{ opacity: 0, width: 0 }}
                className="overflow-hidden"
              >
                <span className="text-sm font-bold text-gradient block leading-tight">Anti-Cheat V6</span>
                <span className="text-[10px] text-white/30 font-mono">Secured Connection</span>
              </motion.div>
            )}
          </AnimatePresence>
        </motion.div>
      </div>

      <div className="flex-1 overflow-y-auto overflow-x-hidden py-3 px-2 scrollbar-hide">
        {displaySections.map((section) => (
          <div key={section.title} className="mb-4">
            <AnimatePresence>
              {!sidebarCollapsed && (
                <motion.p
                  initial={{ opacity: 0 }}
                  animate={{ opacity: 1 }}
                  exit={{ opacity: 0 }}
                  className="text-[10px] uppercase tracking-widest text-white/20 px-3 mb-2 font-semibold"
                >
                  {section.title}
                </motion.p>
              )}
            </AnimatePresence>
            <div className="space-y-0.5">
              {section.items.map((item) => {
                const isActive = location.pathname === item.path || location.pathname.startsWith(item.path + '/');
                return (
                  <NavLink
                    key={item.path}
                    to={item.path}
                    className="block"
                  >
                    <motion.div
                      whileHover={{ x: 4 }}
                      transition={{ type: 'spring', stiffness: 400, damping: 25 }}
                      className={`flex items-center gap-3 px-3 py-2.5 rounded-lg transition-all duration-200 relative group ${
                        isActive
                          ? 'bg-primary-500/15 text-primary-300 border border-primary-500/20'
                          : 'text-white/40 hover:text-white/70 hover:bg-white/[0.04] border border-transparent'
                      }`}
                    >
                      <span className="flex-shrink-0">{item.icon}</span>
                      <AnimatePresence>
                        {!sidebarCollapsed && (
                          <motion.span
                            variants={itemVariants}
                            initial="collapsed"
                            animate="expanded"
                            exit="collapsed"
                            className="text-sm flex-1 truncate"
                          >
                            {item.label}
                          </motion.span>
                        )}
                      </AnimatePresence>
                      {!sidebarCollapsed && item.badge && (
                        <span className="text-[10px] font-bold px-1.5 py-0.5 rounded-full bg-rose-500/20 text-rose-400 flex-shrink-0">
                          {item.badge}
                        </span>
                      )}
                      {isActive && (
                        <motion.div
                          layoutId="activeNav"
                          className="absolute left-0 top-1/2 -translate-y-1/2 w-0.5 h-5 bg-primary-400 rounded-full"
                          transition={{ type: 'spring', stiffness: 300, damping: 30 }}
                        />
                      )}
                    </motion.div>
                  </NavLink>
                );
              })}
            </div>
          </div>
        ))}

        {role !== 'player' && !sidebarCollapsed && onlineMods.length > 0 && (
          <div className="px-2 mt-2">
            <p className="text-[10px] uppercase tracking-widest text-white/20 px-3 mb-2 font-semibold">Online Staff</p>
            <div className="space-y-1.5">
              {onlineMods.slice(0, 5).map((mod) => (
                <div key={mod.name} className="flex items-center gap-2.5 px-3 py-1.5">
                  <div className={`w-1.5 h-1.5 rounded-full ${
                    mod.status === 'online' ? 'bg-emerald-500' : mod.status === 'idle' ? 'bg-amber-500' : 'bg-rose-500'
                  }`} />
                  <span className="text-xs text-white/40">{mod.name}</span>
                </div>
              ))}
              {onlineMods.length > 5 && (
                <p className="text-[10px] text-white/20 px-3">+{onlineMods.length - 5} more</p>
              )}
            </div>
          </div>
        )}
      </div>

      <div className="border-t border-white/5 p-3 space-y-2">
        {role !== 'player' && sidebarCollapsed && onlineMods.length > 0 && (
          <div className="flex items-center justify-center">
            <div className="relative">
              <Users size={14} className="text-white/30" />
              <span className="absolute -top-1 -right-1.5 w-2 h-2 rounded-full bg-emerald-500 border border-[#0f172a]" />
            </div>
          </div>
        )}
        <motion.button
          whileHover={{ scale: 1.05 }}
          whileTap={{ scale: 0.95 }}
          onClick={() => setSidebarCollapsed(!sidebarCollapsed)}
          className="w-full flex items-center justify-center gap-2 py-2 rounded-lg text-white/30 hover:text-white/60 hover:bg-white/5 transition-all"
        >
          {sidebarCollapsed ? <ChevronRight size={16} /> : <><ChevronLeft size={16} /><span className="text-xs">Collapse</span></>}
        </motion.button>
      </div>
    </motion.aside>
  );
}
