import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { BarChart3, Users, Shield, Activity, AlertTriangle } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { adminApi } from '../../services/admin';
import type { IAdminStats, IWeeklyActivity, IThreatDistribution, ITopReport } from '../../services/admin';

export function AnalyticsPage() {
  const [stats, setStats] = useState<IAdminStats | null>(null);
  const [weekly, setWeekly] = useState<IWeeklyActivity[]>([]);
  const [threats, setThreats] = useState<IThreatDistribution[]>([]);
  const [topReports, setTopReports] = useState<ITopReport[]>([]);

  useEffect(() => {
    const fetch = () => {
      adminApi.getStats().then(({ data }) => setStats(data)).catch(() => {});
      adminApi.getWeeklyActivity().then(({ data }) => setWeekly(data)).catch(() => {});
      adminApi.getThreatDistribution().then(({ data }) => setThreats(data)).catch(() => {});
      adminApi.getTopReports().then(({ data }) => setTopReports(data)).catch(() => {});
    };
    fetch();
    const interval = setInterval(fetch, 30000);
    return () => clearInterval(interval);
  }, []);

  const maxScans = Math.max(...weekly.map((d) => d.scans), 1);
  const maxPlayers = Math.max(...weekly.map((d) => d.players), 1);
  const totalScans = weekly.reduce((s, d) => s + d.scans, 0);
  const totalThreats = weekly.reduce((s, d) => s + d.threats, 0);
  const avgPlayers = weekly.length ? Math.round(weekly.reduce((s, d) => s + d.players, 0) / weekly.length) : 0;

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Analytics</h1>
        <p className="text-sm text-white/30 mt-0.5">Detection and usage statistics</p>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Weekly Scans" value={totalScans.toLocaleString()} trend="up" icon={<Activity size={16} />} />
        <MetricCard title="Threats Blocked" value={String(totalThreats)} trend="down" icon={<Shield size={16} />} />
        <MetricCard title="Avg Players" value={avgPlayers.toLocaleString()} trend="up" icon={<Users size={16} />} />
        <MetricCard title="Detection Rate" value={stats ? `${stats.detectionRate}%` : '—'} trend="up" icon={<BarChart3 size={16} />} />
      </div>

      <GlassCard className="p-6">
        <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-6">Weekly Activity</h3>
        <div className="space-y-4">
          {weekly.map((d) => (
            <div key={d.day} className="space-y-1">
              <div className="flex items-center justify-between text-xs">
                <span className="text-white/40 w-8">{d.day}</span>
                <div className="flex-1 mx-4 space-y-1">
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-2 rounded-full bg-white/5 overflow-hidden">
                      <motion.div
                        initial={{ width: 0 }}
                        animate={{ width: `${(d.scans / maxScans) * 100}%` }}
                        transition={{ duration: 0.8, delay: 0.1 }}
                        className="h-full rounded-full bg-primary-500/60"
                      />
                    </div>
                    <span className="text-[10px] text-white/30 w-12 text-right font-mono">{d.scans}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <div className="flex-1 h-1.5 rounded-full bg-white/5 overflow-hidden">
                      <motion.div
                        initial={{ width: 0 }}
                        animate={{ width: `${(d.players / maxPlayers) * 100}%` }}
                        transition={{ duration: 0.8, delay: 0.2 }}
                        className="h-full rounded-full bg-emerald-500/40"
                      />
                    </div>
                    <span className="text-[10px] text-white/20 w-12 text-right font-mono">{d.players}</span>
                  </div>
                </div>
                <div className="flex items-center gap-1 text-[10px]">
                  {d.threats > 0 ? <AlertTriangle size={10} className="text-amber-400" /> : <Shield size={10} className="text-emerald-400" />}
                  <span className={d.threats > 0 ? 'text-amber-400' : 'text-emerald-400'}>{d.threats}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </GlassCard>

      <div className="grid grid-cols-2 gap-6">
        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Threat Distribution</h3>
          <div className="space-y-3">
            {threats.map((t) => (
              <div key={t.type} className="space-y-1">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-white/50">{t.type}</span>
                  <span className="text-white/30 font-mono">{t.count} ({t.pct}%)</span>
                </div>
                <div className="h-1.5 rounded-full bg-white/5 overflow-hidden">
                  <motion.div
                    initial={{ width: 0 }}
                    animate={{ width: `${t.pct}%` }}
                    transition={{ duration: 0.6, delay: 0.1 }}
                    className="h-full rounded-full bg-rose-500/60"
                  />
                </div>
              </div>
            ))}
          </div>
        </GlassCard>

        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Top Reports</h3>
          <div className="space-y-3">
            {topReports.map((p) => (
              <div key={p.player} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
                <div>
                  <p className="text-xs text-white/60">{p.player}</p>
                  <p className="text-[10px] text-white/30">{p.reports} reports</p>
                </div>
                <span className={`text-[10px] px-2 py-0.5 rounded-full ${
                  p.action === 'Banned' ? 'bg-rose-500/20 text-rose-400' :
                  p.action === 'Investigated' ? 'bg-amber-500/20 text-amber-400' :
                  'bg-primary-500/20 text-primary-300'
                }`}>{p.action}</span>
              </div>
            ))}
          </div>
        </GlassCard>
      </div>
    </div>
  );
}
