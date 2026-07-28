import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Users, Search, X } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { UserCard } from '../../components/ui/UserCard';
import { SearchBar } from '../../components/ui/SearchBar';
import { moderatorApi } from '../../services/moderator';
import type { IPlayerSearchResult } from '../../services/moderator';

export function PlayerSearchPage() {
  const navigate = useNavigate();
  const [query, setQuery] = useState('');
  const [emailFilter, setEmailFilter] = useState('');
  const [statusFilter, setStatusFilter] = useState('');
  const [minReports, setMinReports] = useState('');
  const [maxReports, setMaxReports] = useState('');
  const [players, setPlayers] = useState<IPlayerSearchResult[]>([]);
  const [showFilters, setShowFilters] = useState(false);

  useEffect(() => {
    const params: { q?: string; email?: string; status?: string; minReports?: number; maxReports?: number } = {};
    if (query) params.q = query;
    if (emailFilter) params.email = emailFilter;
    if (statusFilter) params.status = statusFilter;
    if (minReports) params.minReports = Number(minReports);
    if (maxReports) params.maxReports = Number(maxReports);
    moderatorApi.searchPlayers(params).then(({ data }) => setPlayers(data)).catch((err) => console.error('[PlayerSearchPage] failed to fetch', err));
  }, [query, emailFilter, statusFilter, minReports, maxReports]);

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-xl font-bold text-white">Player Search</h1>
            <p className="text-sm text-white/30 mt-0.5">Find and inspect player profiles</p>
          </div>
          <button onClick={() => setShowFilters(!showFilters)} className="flex items-center gap-1.5 text-xs text-white/30 hover:text-white/60 transition-all">
            {showFilters ? <X size={14} /> : <Search size={14} />}
            {showFilters ? 'Hide Filters' : 'Filters'}
          </button>
        </div>
      </motion.div>

      <GlassCard className="p-6">
        <div className="max-w-md">
          <SearchBar
            value={query}
            onChange={setQuery}
            placeholder="Search by username..."
          />
        </div>
        {showFilters && (
          <div className="mt-4 pt-4 border-t border-white/5 grid grid-cols-4 gap-3">
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1">Email</label>
              <input value={emailFilter} onChange={(e) => setEmailFilter(e.target.value)}
                className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-xs text-white/60 placeholder-white/20 outline-none focus:border-primary-500/30"
                placeholder="Filter by email..." />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1">Status</label>
              <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)}
                className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-xs text-white/60 outline-none focus:border-primary-500/30">
                <option value="">All</option>
                <option value="online">Online</option>
                <option value="offline">Offline</option>
                <option value="suspected">Suspected</option>
              </select>
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1">Min Reports</label>
              <input value={minReports} onChange={(e) => setMinReports(e.target.value)} type="number" min="0"
                className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-xs text-white/60 placeholder-white/20 outline-none focus:border-primary-500/30"
                placeholder="0" />
            </div>
            <div>
              <label className="text-[10px] text-white/40 uppercase tracking-wider block mb-1">Max Reports</label>
              <input value={maxReports} onChange={(e) => setMaxReports(e.target.value)} type="number" min="0"
                className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-xs text-white/60 placeholder-white/20 outline-none focus:border-primary-500/30"
                placeholder="100" />
            </div>
          </div>
        )}
      </GlassCard>

      {players.length === 0 && (query || emailFilter || statusFilter || minReports || maxReports) && (
        <div className="text-center py-12">
          <Users size={40} className="text-white/10 mx-auto mb-3" />
          <p className="text-sm text-white/20">No players found matching your criteria</p>
        </div>
      )}

      <div className="grid grid-cols-2 gap-4">
        {players.map((player) => (
          <UserCard
            key={player.id}
            player={{
              id: player.id,
              username: player.username,
              email: player.email,
              trustScore: player.trustScore,
              status: player.status as 'online' | 'offline' | 'suspected',
              lastSeen: player.lastSeen,
              gameName: player.gameName,
              hoursPlayed: player.hoursPlayed,
              reportsCount: player.reportsCount,
              bansCount: player.bansCount,
              avatar: player.avatar,
            }}
            onClick={() => navigate(`/moderator/players/${player.id}`)}
          />
        ))}
      </div>
    </div>
  );
}
