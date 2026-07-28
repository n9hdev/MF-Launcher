import { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Search, Command, ArrowRight } from 'lucide-react';
import { useUIStore } from '../../stores/uiStore';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../../stores/authStore';
import type { UserRole } from '../../types/global';

interface ICommand {
  id: string;
  label: string;
  category: string;
  path: string | null;
  icon: string;
  roles: UserRole[];
}

const allCommands: ICommand[] = [
  { id: '1', label: 'Go to Dashboard', category: 'Navigation', path: '/dashboard', icon: 'LayoutDashboard', roles: ['player', 'moderator', 'admin', 'superadmin'] },
  { id: '2', label: 'Open Protection', category: 'Navigation', path: '/player/protection', icon: 'Shield', roles: ['player'] },
  { id: '3', label: 'View History', category: 'Navigation', path: '/player/history', icon: 'Clock', roles: ['player'] },
  { id: '4', label: 'Launch Game', category: 'Actions', path: '/player/launch', icon: 'Gamepad2', roles: ['player'] },
  { id: '5', label: 'Run System Scan', category: 'Actions', path: '/player/protection', icon: 'Activity', roles: ['player'] },
  { id: '6', label: 'Open Settings', category: 'Navigation', path: '/settings', icon: 'Settings', roles: ['player', 'moderator', 'admin', 'superadmin'] },
  { id: '7', label: 'View Notifications', category: 'Navigation', path: null, icon: 'Bell', roles: ['player', 'moderator', 'admin', 'superadmin'] },
  { id: '8', label: 'Toggle Theme', category: 'Actions', path: null, icon: 'Sun', roles: ['player', 'moderator', 'admin', 'superadmin'] },
  { id: '9', label: 'Moderator Dashboard', category: 'Moderation', path: '/moderator/dashboard', icon: 'LayoutDashboard', roles: ['moderator', 'admin', 'superadmin'] },
  { id: '10', label: 'Reports Queue', category: 'Moderation', path: '/moderator/reports', icon: 'Flag', roles: ['moderator', 'admin', 'superadmin'] },
  { id: '11', label: 'Player Search', category: 'Moderation', path: '/moderator/players', icon: 'Search', roles: ['moderator', 'admin', 'superadmin'] },
  { id: '12', label: 'Active Alerts', category: 'Moderation', path: '/moderator/alerts', icon: 'Bell', roles: ['moderator', 'admin', 'superadmin'] },
  { id: '13', label: 'Mod Chat', category: 'Moderation', path: '/moderator/chat', icon: 'MessageSquare', roles: ['moderator', 'admin', 'superadmin'] },
  { id: '14', label: 'Ban Center', category: 'Administration', path: '/admin/bans', icon: 'Gavel', roles: ['admin', 'superadmin'] },
  { id: '15', label: 'Analytics', category: 'Administration', path: '/admin/analytics', icon: 'BarChart3', roles: ['admin', 'superadmin'] },
  { id: '16', label: 'Appeals', category: 'Administration', path: '/admin/appeals', icon: 'ScrollText', roles: ['admin', 'superadmin'] },
  { id: '17', label: 'Whitelist', category: 'Administration', path: '/admin/whitelist', icon: 'UserCheck', roles: ['admin', 'superadmin'] },
  { id: '18', label: 'Live Player View', category: 'Administration', path: '/admin/live-view', icon: 'Monitor', roles: ['admin', 'superadmin'] },
  { id: '19', label: 'Command Center', category: 'SuperAdmin', path: '/superadmin/command', icon: 'Terminal', roles: ['superadmin'] },
  { id: '20', label: 'Telemetry', category: 'SuperAdmin', path: '/superadmin/telemetry', icon: 'Activity', roles: ['superadmin'] },
  { id: '21', label: 'Detection Center', category: 'SuperAdmin', path: '/superadmin/detection', icon: 'Microscope', roles: ['superadmin'] },
  { id: '22', label: 'Rule Engine', category: 'SuperAdmin', path: '/superadmin/rules', icon: 'BookOpen', roles: ['superadmin'] },
  { id: '23', label: 'Infrastructure', category: 'SuperAdmin', path: '/superadmin/infrastructure', icon: 'Server', roles: ['superadmin'] },
  { id: '24', label: 'Audit Log', category: 'SuperAdmin', path: '/superadmin/audit', icon: 'FileSearch', roles: ['superadmin'] },
];

export function CommandPalette() {
  const { commandPaletteOpen, toggleCommandPalette } = useUIStore();
  const { user } = useAuthStore();
  const [query, setQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);
  const navigate = useNavigate();

  const role = user?.role || 'player';
  const commands = allCommands.filter((c) => c.roles.includes(role));

  const filtered = commands.filter(
    (c) => c.label.toLowerCase().includes(query.toLowerCase()) || c.category.toLowerCase().includes(query.toLowerCase())
  );

  useEffect(() => {
    if (commandPaletteOpen) {
      setQuery('');
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [commandPaletteOpen]);

  const handleSelect = (index: number) => {
    const cmd = filtered[index];
    if (!cmd) return;
    if (cmd.path) navigate(cmd.path);
    toggleCommandPalette();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'ArrowDown') { e.preventDefault(); setSelectedIndex((i) => Math.min(i + 1, filtered.length - 1)); }
    if (e.key === 'ArrowUp') { e.preventDefault(); setSelectedIndex((i) => Math.max(i - 1, 0)); }
    if (e.key === 'Enter') { e.preventDefault(); handleSelect(selectedIndex); }
    if (e.key === 'Escape') toggleCommandPalette();
  };

  return (
    <AnimatePresence>
      {commandPaletteOpen && (
        <>
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/40 z-50"
            onClick={toggleCommandPalette}
          />
          <motion.div
            initial={{ opacity: 0, y: -20, scale: 0.96 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -20, scale: 0.96 }}
            transition={{ type: 'spring', stiffness: 300, damping: 25 }}
            className="fixed top-[15%] left-1/2 -translate-x-1/2 w-full max-w-lg z-50 rounded-xl overflow-hidden"
            style={{ background: 'rgba(15, 23, 42, 0.95)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
          >
            <div className="flex items-center gap-3 px-4 py-3 border-b border-white/5">
              <Search size={16} className="text-white/30" />
              <input
                ref={inputRef}
                value={query}
                onChange={(e) => { setQuery(e.target.value); setSelectedIndex(0); }}
                onKeyDown={handleKeyDown}
                placeholder="Type a command or page..."
                className="flex-1 bg-transparent text-sm text-white/80 placeholder-white/30 outline-none"
              />
              <kbd className="text-[10px] px-1.5 py-0.5 rounded bg-white/5 text-white/20 font-mono">ESC</kbd>
            </div>
            <div className="max-h-72 overflow-y-auto p-2">
              {filtered.length === 0 && (
                <p className="text-sm text-white/30 text-center py-8">No results found</p>
              )}
              {filtered.map((cmd, i) => (
                <motion.button
                  key={cmd.id}
                  initial={{ opacity: 0, x: -10 }}
                  animate={{ opacity: 1, x: 0 }}
                  transition={{ delay: i * 0.03 }}
                  onClick={() => handleSelect(i)}
                  className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm transition-all ${
                    i === selectedIndex ? 'bg-primary-500/15 text-primary-300' : 'text-white/50 hover:text-white/70 hover:bg-white/5'
                  }`}
                >
                  <ArrowRight size={14} className={i === selectedIndex ? 'opacity-100' : 'opacity-0'} />
                  <span className="flex-1 text-left">{cmd.label}</span>
                  <span className="text-[10px] text-white/20">{cmd.category}</span>
                </motion.button>
              ))}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
