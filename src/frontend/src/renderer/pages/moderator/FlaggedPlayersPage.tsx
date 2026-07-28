import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import { Flag, Search, ExternalLink, Shield, Clock, CheckCircle, XCircle, Eye, AlertTriangle, FlagOff } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { moderatorApi } from '../../services/moderator';
import type { IPlayerReport } from '../../services/reports';
import { useUIStore } from '../../stores/uiStore';

const statusColor = (status: string) => {
  const s = status?.toLowerCase();
  const map: Record<string, string> = {
    pending: 'bg-primary-500/20 text-primary-300',
    open: 'bg-primary-500/20 text-primary-300',
    investigating: 'bg-amber-500/20 text-amber-400',
    inprogress: 'bg-amber-500/20 text-amber-400',
    resolved: 'bg-emerald-500/20 text-emerald-400',
    dismissed: 'bg-white/10 text-white/40',
  };
  return map[s] || 'bg-white/10 text-white/40';
};

const statusLabel = (status: string) => {
  const s = status?.toLowerCase();
  if (s === 'pending' || s === 'open') return 'Open';
  if (s === 'investigating' || s === 'inprogress') return 'In Progress';
  return status?.charAt(0).toUpperCase() + status?.slice(1);
};

export function FlaggedPlayersPage() {
  const navigate = useNavigate();
  const { addToast } = useUIStore();
  const [reports, setReports] = useState<IPlayerReport[]>([]);
  const [search, setSearch] = useState('');
  const [filterStatus, setFilterStatus] = useState('all');
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  useEffect(() => {
    const fetch = () => moderatorApi.getFlaggedPlayerReports().then(({ data }) => setReports(data)).catch(() => {});
    fetch();
    const interval = setInterval(fetch, 15000);
    return () => clearInterval(interval);
  }, []);

  const filtered = reports.filter((r) => {
    const s = r.status?.toLowerCase();
    if (filterStatus !== 'all') {
      if (filterStatus === 'open' && s !== 'pending' && s !== 'open') return false;
      if (filterStatus === 'investigating' && s !== 'investigating' && s !== 'inprogress') return false;
      if (filterStatus === 'resolved' && s !== 'resolved') return false;
      if (filterStatus === 'dismissed' && s !== 'dismissed') return false;
    }
    if (!search) return true;
    const q = search.toLowerCase();
    return r.playerName?.toLowerCase().includes(q) || r.reason?.toLowerCase().includes(q);
  });

  const handleStatusChange = async (reportId: string, newStatus: string) => {
    setActionLoading(reportId);
    try {
      await moderatorApi.updatePlayerReportStatus(reportId, newStatus);
      setReports((prev) => prev.map((r) => r.id === reportId ? { ...r, status: newStatus } : r));
      addToast({ type: 'success', title: 'Status Updated', message: `Report marked as ${statusLabel(newStatus)}` });
    } catch {
      addToast({ type: 'error', title: 'Failed to update status' });
    } finally {
      setActionLoading(null);
    }
  };

  const handleUnflag = async (reportId: string) => {
    setActionLoading(reportId);
    try {
      await moderatorApi.flagPlayerReport(reportId, false);
      setReports((prev) => prev.filter((r) => r.id !== reportId));
      addToast({ type: 'success', title: 'Unflagged', message: 'Report removed from flagged list' });
    } catch {
      addToast({ type: 'error', title: 'Failed to unflag report' });
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Flagged Players</h1>
          <p className="text-sm text-white/30 mt-0.5">Reports flagged for cheating or suspicious behavior</p>
        </div>
        <div className="flex items-center gap-2 text-xs text-white/30">
          <AlertTriangle size={14} className="text-amber-400" />
          <span>{filtered.length} flagged</span>
        </div>
      </motion.div>

      <div className="flex items-center gap-3">
        <div className="relative flex-1 max-w-md">
          <Search size={14} className="absolute left-3 top-1/2 -translate-y-1/2 text-white/20" />
          <input value={search} onChange={(e) => setSearch(e.target.value)}
            className="w-full bg-white/5 border border-white/10 rounded-xl pl-9 pr-4 py-2.5 text-xs text-white/60 placeholder-white/20 outline-none focus:border-primary-500/40"
            placeholder="Search by player name or reason..." />
        </div>
        <div className="flex gap-2">
          {['all', 'open', 'investigating', 'resolved', 'dismissed'].map((s) => (
            <button key={s} onClick={() => setFilterStatus(s)}
              className={`text-xs px-3 py-1.5 rounded-lg transition-all ${
                filterStatus === s ? 'bg-primary-500/20 text-primary-300 border border-primary-500/20' : 'text-white/30 hover:text-white/50 border border-transparent'
              }`}>{s === 'all' ? 'All' : statusLabel(s)}</button>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        {filtered.map((r) => (
          <GlassCard key={r.id} className="p-4 hover:border-white/10 transition-colors">
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-4 flex-1 min-w-0">
                <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-rose-500/20 to-primary-500/20 flex items-center justify-center flex-shrink-0">
                  <Flag size={16} className="text-rose-400" />
                </div>
                <div className="min-w-0">
                  <div className="flex items-center gap-2">
                    <p className="text-sm font-medium text-white/80 truncate">{r.playerName}</p>
                    <span className={`text-[10px] px-1.5 py-0.5 rounded-full font-medium ${statusColor(r.status)}`}>
                      {statusLabel(r.status)}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 mt-1">
                    <p className="text-xs text-white/40">{r.reason}</p>
                    {r.description && (
                      <p className="text-[10px] text-white/20 truncate max-w-[200px]">— {r.description}</p>
                    )}
                  </div>
                  <div className="flex items-center gap-3 mt-1 text-[10px] text-white/20">
                    <span className="flex items-center gap-1">
                      <Clock size={10} />
                      {new Date(r.createdAt).toLocaleDateString()}
                    </span>
                    {r.reporterId && <span>Reported by: {r.reporterId.slice(0, 8)}…</span>}
                  </div>
                </div>
              </div>

              <div className="flex items-center gap-2 shrink-0 ml-4">
                {(r.status?.toLowerCase() === 'pending' || r.status?.toLowerCase() === 'open') && (
                  <>
                    <AnimatedButton
                      variant="secondary"
                      size="sm"
                      icon={<Eye size={12} />}
                      disabled={actionLoading === r.id}
                      onClick={() => handleStatusChange(r.id, 'investigating')}
                    >
                      Investigate
                    </AnimatedButton>
                    <AnimatedButton
                      variant="secondary"
                      size="sm"
                      icon={<CheckCircle size={12} />}
                      disabled={actionLoading === r.id}
                      onClick={() => handleStatusChange(r.id, 'resolved')}
                    >
                      Resolve
                    </AnimatedButton>
                  </>
                )}
                <AnimatedButton
                  variant="secondary"
                  size="sm"
                  icon={<FlagOff size={12} />}
                  disabled={actionLoading === r.id}
                  onClick={() => handleUnflag(r.id)}
                >
                  Unflag
                </AnimatedButton>
                <AnimatedButton
                  variant="gradient"
                  size="sm"
                  icon={<ExternalLink size={12} />}
                  onClick={() => navigate(`/moderator/flagged/${r.id}`)}
                >
                  Investigate
                </AnimatedButton>
              </div>
            </div>
          </GlassCard>
        ))}
        {filtered.length === 0 && (
          <GlassCard className="p-12 text-center">
            <Flag size={32} className="mx-auto text-white/10 mb-3" />
            <p className="text-sm text-white/20">No flagged players found</p>
          </GlassCard>
        )}
      </div>
    </div>
  );
}
