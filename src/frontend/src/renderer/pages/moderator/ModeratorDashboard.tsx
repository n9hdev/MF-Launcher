import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { Flag, Users, Bell, Shield, Search, ArrowRight } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { moderatorApi } from '../../services/moderator';
import type { IPlayerReport } from '../../services/reports';
import type { IModeratorStats, IActiveAlert } from '../../services/moderator';

export function ModeratorDashboard() {
  const navigate = useNavigate();
  const [reports, setReports] = useState<IPlayerReport[]>([]);
  const [stats, setStats] = useState<IModeratorStats | null>(null);
  const [activeAlerts, setActiveAlerts] = useState<IActiveAlert[]>([]);

  useEffect(() => {
    const fetch = () => {
      moderatorApi.getAllPlayerReports().then(({ data }) => setReports(data)).catch(() => {});
      moderatorApi.getStats().then(({ data }) => setStats(data)).catch(() => {});
      moderatorApi.getActiveAlerts().then(({ data }) => setActiveAlerts(data)).catch(() => {});
    };
    fetch();
    const interval = setInterval(fetch, 15000);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Moderator Dashboard</h1>
          <p className="text-sm text-white/30 mt-0.5">Report queue & player monitoring</p>
        </div>
        <AnimatedButton variant="gradient" icon={<Search size={14} />} onClick={() => navigate('/moderator/players')}>Quick Search</AnimatedButton>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Open Reports" value={String(stats?.openReports ?? '—')} trend="up" trendValue="+3" subtitle="Awaiting review" icon={<Flag size={16} />} />
        <MetricCard title="Active Players" value={stats?.activePlayers?.toLocaleString() ?? '—'} subtitle="Currently online" icon={<Users size={16} />} />
        <MetricCard title="Active Alerts" value={String(stats?.activeAlerts ?? '—')} trend="up" subtitle="Requires attention" icon={<Bell size={16} />} />
        <MetricCard title="Resolved Today" value={String(stats?.resolvedToday ?? '—')} trend="up" subtitle="Reports closed" icon={<Shield size={16} />} />
      </div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Recent Reports</h3>
              <button onClick={() => navigate('/moderator/reports')} className="text-[10px] text-primary-400 hover:text-primary-300 flex items-center gap-1">View all <ArrowRight size={10} /></button>
            </div>
            <div className="space-y-2">
              {reports.slice(0, 5).map((r) => (
                <div key={r.id} className="flex items-center justify-between p-3 rounded-xl hover:bg-white/[0.03] transition-colors border border-transparent hover:border-white/5">
                  <div className="flex items-center gap-3">
                    <div className={`w-2 h-2 rounded-full ${r.status === 'pending' ? 'bg-amber-500' : r.status === 'investigating' ? 'bg-primary-500' : 'bg-emerald-500'}`} />
                    <div>
                      <p className="text-sm text-white/70">{r.playerName || r.reason}</p>
                      <p className="text-[10px] text-white/30">{r.reason} &middot; {new Date(r.createdAt).toLocaleDateString()}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-3">
                    <span className={`text-[10px] px-2 py-0.5 rounded-full capitalize ${
                      r.status === 'pending' ? 'bg-amber-500/20 text-amber-400' :
                      r.status === 'investigating' ? 'bg-primary-500/20 text-primary-300' :
                      r.status === 'resolved' ? 'bg-emerald-500/20 text-emerald-400' :
                      'bg-white/5 text-white/30'
                    }`}>{r.status}</span>
                    <span className="text-[10px] text-white/20">{new Date(r.createdAt).toLocaleDateString()}</span>
                    <AnimatedButton size="sm" variant="secondary" onClick={() => navigate(`/moderator/reports/${r.id}`)}>Review</AnimatedButton>
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Active Alerts</h3>
            <div className="space-y-2">
              {activeAlerts.map((a) => (
                <div key={a.playerName + a.type} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
                  <div>
                    <p className="text-xs text-white/60">{a.type}</p>
                    <p className="text-[10px] text-white/30">{a.playerName}</p>
                  </div>
                  <div className="text-right">
                    <span className={`text-[10px] ${
                      a.severity === 'high' || a.severity === 'critical' ? 'text-rose-400' : a.severity === 'medium' ? 'text-amber-400' : 'text-primary-300'
                    }`}>{a.severity}</span>
                    <p className="text-[10px] text-white/20">{a.timeAgo}</p>
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Quick Stats</h3>
            <div className="space-y-2">
              {[
                { label: 'Avg response time', value: `${stats?.avgResponseTime ?? '—'}m` },
                { label: 'Reports / hour', value: String(stats?.reportsPerHour ?? '—') },
                { label: 'Ban accuracy', value: `${stats?.banAccuracy ?? '—'}%` },
              ].map((s) => (
                <div key={s.label} className="flex items-center justify-between text-xs">
                  <span className="text-white/30">{s.label}</span>
                  <span className="text-white/60 font-mono">{s.value}</span>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
