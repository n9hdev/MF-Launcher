import { useState, useEffect, useRef, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Search, Users, Shield, FileText, X, Flag, Clock } from 'lucide-react';
import { useUIStore } from '../../stores/uiStore';
import { useNavigate } from 'react-router-dom';
import { moderatorApi } from '../../services/moderator';
import { useAuthStore } from '../../stores/authStore';
import type { IPlayerSearchResult } from '../../services/moderator';

interface ISearchResult {
  id: string;
  label: string;
  description: string;
  category: string;
  icon: React.ElementType;
  path: string;
}

const pageResults: ISearchResult[] = [
  { id: 'p1', label: 'Protection Status', description: 'View active protection modules', category: 'Pages', icon: Shield, path: '/player/protection' },
  { id: 'p2', label: 'Game Launch', description: 'Launch MTA:SA', category: 'Pages', icon: Shield, path: '/player/launch' },
  { id: 'p3', label: 'Detection History', description: 'View past detection events', category: 'Pages', icon: Clock, path: '/player/history' },
  { id: 'p4', label: 'Player Reports', description: 'Submit and track player reports', category: 'Pages', icon: Flag, path: '/player/reports' },
  { id: 'p5', label: 'Reports Queue', description: 'Manage incoming player reports', category: 'Pages', icon: Flag, path: '/moderator/reports' },
  { id: 'p6', label: 'Player Search', description: 'Search and inspect players', category: 'Pages', icon: Users, path: '/moderator/players' },
  { id: 'p7', label: 'Active Alerts', description: 'View security alerts', category: 'Pages', icon: Shield, path: '/moderator/alerts' },
  { id: 'p8', label: 'Mod Chat', description: 'Team communication', category: 'Pages', icon: Shield, path: '/moderator/chat' },
  { id: 'p9', label: 'Ban Center', description: 'Manage player bans', category: 'Pages', icon: FileText, path: '/admin/bans' },
  { id: 'p10', label: 'Analytics', description: 'Detection and usage statistics', category: 'Pages', icon: FileText, path: '/admin/analytics' },
  { id: 'p11', label: 'Appeals', description: 'Review player appeals', category: 'Pages', icon: FileText, path: '/admin/appeals' },
  { id: 'p12', label: 'Whitelist', description: 'Manage whitelist entries', category: 'Pages', icon: Shield, path: '/admin/whitelist' },
  { id: 'p13', label: 'Settings', description: 'Configure application settings', category: 'Pages', icon: Shield, path: '/settings' },
];

export function GlobalSearch() {
  const { searchOpen, toggleSearch } = useUIStore();
  const { user } = useAuthStore();
  const [query, setQuery] = useState('');
  const [playerResults, setPlayerResults] = useState<IPlayerSearchResult[]>([]);
  const [searching, setSearching] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout>>();
  const navigate = useNavigate();

  const role = user?.role || 'player';

  useEffect(() => {
    if (searchOpen) {
      setQuery('');
      setPlayerResults([]);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [searchOpen]);

  const doSearch = useCallback(async (q: string) => {
    if (q.length < 2) { setPlayerResults([]); return; }
    setSearching(true);
    try {
      const { data } = await moderatorApi.searchPlayers({ q });
      setPlayerResults(data.slice(0, 5));
    } catch { setPlayerResults([]); }
    setSearching(false);
  }, []);

  useEffect(() => {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => doSearch(query), 250);
    return () => { if (debounceRef.current) clearTimeout(debounceRef.current); };
  }, [query, doSearch]);

  const filteredPages = query.length < 2
    ? pageResults
    : pageResults.filter(
        (r) => r.label.toLowerCase().includes(query.toLowerCase()) || r.description.toLowerCase().includes(query.toLowerCase())
      );

  const handleNavigate = (path: string) => {
    navigate(path);
    toggleSearch();
  };

  return (
    <AnimatePresence>
      {searchOpen && (
        <>
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/30 z-50"
            onClick={toggleSearch}
          />
          <motion.div
            initial={{ opacity: 0, y: -10, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            exit={{ opacity: 0, y: -10, scale: 0.97 }}
            transition={{ type: 'spring', stiffness: 300, damping: 25 }}
            className="fixed top-20 left-1/2 -translate-x-1/2 w-full max-w-xl z-50 rounded-xl overflow-hidden"
            style={{ background: 'rgba(15, 23, 42, 0.95)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
          >
            <div className="flex items-center gap-3 px-4 py-3 border-b border-white/5">
              <Search size={16} className="text-white/30" />
              <input
                ref={inputRef}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder="Search players, pages, settings..."
                className="flex-1 bg-transparent text-sm text-white/80 placeholder-white/30 outline-none"
              />
              {searching && <div className="w-4 h-4 rounded-full border-2 border-primary-500/30 border-t-primary-500 animate-spin" />}
              <button onClick={toggleSearch} className="text-white/20 hover:text-white/50">
                <X size={14} />
              </button>
            </div>
            <div className="max-h-80 overflow-y-auto p-2">
              {playerResults.length > 0 && (
                <div className="mb-2">
                  <p className="text-[10px] uppercase tracking-widest text-white/20 px-3 py-1.5 font-semibold">Players</p>
                  {playerResults.map((p) => (
                    <button
                      key={p.id}
                      onClick={() => handleNavigate(`/moderator/players/${p.id}`)}
                      className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg hover:bg-white/5 text-white/50 hover:text-white/80 transition-all"
                    >
                      <Users size={14} />
                      <div className="text-left flex-1">
                        <p className="text-sm">{p.username}</p>
                        <p className="text-[10px] text-white/30">Trust: {p.trustScore}% &middot; {p.status} &middot; {p.reportsCount} reports</p>
                      </div>
                      <span className={`text-[10px] px-1.5 py-0.5 rounded ${
                        p.status === 'online' ? 'bg-emerald-500/20 text-emerald-400' : 'bg-white/5 text-white/30'
                      }`}>{p.status}</span>
                    </button>
                  ))}
                </div>
              )}

              {query.length < 2 && playerResults.length === 0 && (
                <p className="text-xs text-white/20 text-center py-4">Type at least 2 characters to search players</p>
              )}

              {query.length >= 2 && playerResults.length === 0 && !searching && (
                <p className="text-xs text-white/20 text-center py-4">No players found for "{query}"</p>
              )}

              {filteredPages.length > 0 && (role !== 'player' || filteredPages.some(p => !p.path.includes('moderator') && !p.path.includes('admin'))) && (
                <div className="mb-2">
                  <p className="text-[10px] uppercase tracking-widest text-white/20 px-3 py-1.5 font-semibold">Pages</p>
                  {filteredPages
                    .filter((r) => {
                      if (role === 'player' && r.path.includes('moderator')) return false;
                      if ((role === 'moderator' || role === 'player') && r.path.includes('admin')) return false;
                      return true;
                    })
                    .map((result) => (
                      <button
                        key={result.id}
                        onClick={() => handleNavigate(result.path)}
                        className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg hover:bg-white/5 text-white/50 hover:text-white/80 transition-all"
                      >
                        <result.icon size={14} />
                        <div className="text-left">
                          <p className="text-sm">{result.label}</p>
                          <p className="text-[10px] text-white/30">{result.description}</p>
                        </div>
                      </button>
                    ))}
                </div>
              )}

              {query.length >= 2 && playerResults.length === 0 && filteredPages.length === 0 && !searching && (
                <p className="text-sm text-white/30 text-center py-8">No results found</p>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
