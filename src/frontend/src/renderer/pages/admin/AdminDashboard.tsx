import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ShieldAlert, BarChart3, ScrollText, Activity, Users, ArrowRight } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { adminApi } from '../../services/admin';
import { superAdminApi } from '../../services/superadmin';
import { useNotificationStore } from '../../stores/notificationStore';
import { useUIStore } from '../../stores/uiStore';
import type { IAdminStats, IDetectorPerformance, IAdminBanEntry } from '../../services/admin';

export function AdminDashboard() {
  const navigate = useNavigate();
  const { addNotification } = useNotificationStore();
  const { addToast } = useUIStore();
  const [stats, setStats] = useState<IAdminStats | null>(null);
  const [detectors, setDetectors] = useState<IDetectorPerformance[]>([]);
  const [bans, setBans] = useState<IAdminBanEntry[]>([]);

  useEffect(() => {
    const fetch = () => {
      adminApi.getStats().then(({ data }) => setStats(data)).catch(() => {});
      adminApi.getDetectors().then(({ data }) => setDetectors(data)).catch(() => {});
      adminApi.getBans().then(({ data }) => setBans(data.slice(0, 4))).catch(() => {});
    };
    fetch();
    const interval = setInterval(fetch, 30000);
    return () => clearInterval(interval);
  }, []);

  const handleGenerateReport = async () => {
    try {
      const { data } = await superAdminApi.getStats();
      addNotification({ id: `report-${Date.now()}`, type: 'info', title: 'Report Generated', message: `Stats: ${data.totalUsers} users, ${data.activeSessions} sessions`, timestamp: 'Just now', read: false });
      addToast({ type: 'success', title: 'Report Generated', message: 'Statistics report created successfully' });
    } catch {
      addToast({ type: 'error', title: 'Failed', message: 'Could not generate report' });
    }
  };

  const handleExportAuditLog = async () => {
    try {
      const { data } = await superAdminApi.getAuditLogs();
      const headers = ['Action', 'User', 'Target', 'Details', 'Timestamp', 'IP'];
      const csvRows = [headers.join(',')];
      data.forEach((log: { action: string; user: string; target: string; details: string; timestamp: string; ip: string }) => {
        csvRows.push([log.action, log.user, log.target, `"${log.details}"`, log.timestamp, log.ip].join(','));
      });
      const blob = new Blob([csvRows.join('\n')], { type: 'text/csv' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url; a.download = `audit-log-${Date.now()}.csv`; a.click();
      URL.revokeObjectURL(url);
      addToast({ type: 'success', title: 'Exported', message: `${data.length} audit log entries exported as CSV` });
    } catch {
      addToast({ type: 'error', title: 'Failed', message: 'Could not export audit log' });
    }
  };

  const handleUpdateBlacklist = () => {
    addToast({ type: 'info', title: 'Blacklist Update', message: 'Blacklist update initiated' });
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Administrator Dashboard</h1>
          <p className="text-sm text-white/30 mt-0.5">Full system oversight & management</p>
        </div>
        <AnimatedButton variant="gradient" icon={<BarChart3 size={14} />} onClick={handleGenerateReport}>Generate Report</AnimatedButton>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Active Bans" value={String(stats?.activeBans ?? '—')} trend="up" subtitle={`${stats?.permanentBans ?? 0} permanent, ${stats?.temporaryBans ?? 0} temporary`} icon={<ShieldAlert size={16} />} />
        <MetricCard title="Pending Appeals" value={String(stats?.pendingAppeals ?? '—')} trend="down" subtitle="Awaiting review" icon={<ScrollText size={16} />} />
        <MetricCard title="Detection Rate" value={`${stats?.detectionRate ?? '—'}%`} trend="up" subtitle="Last 30 days" icon={<Activity size={16} />} />
        <MetricCard title="Total Players" value={stats?.totalPlayers?.toLocaleString() ?? '—'} trend="up" icon={<Users size={16} />} />
      </div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2 space-y-6">
          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Detector Performance</h3>
              <button onClick={() => navigate('/admin/analytics')} className="text-[10px] text-primary-400 hover:text-primary-300 flex items-center gap-1">Details <ArrowRight size={10} /></button>
            </div>
            <div className="space-y-3">
              {detectors.map((d) => (
                <div key={d.name} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
                  <div className="flex items-center gap-3">
                    <div className={`w-2 h-2 rounded-full ${d.status === 'active' ? 'bg-emerald-500' : 'bg-amber-500'}`} />
                    <span className="text-xs text-white/60">{d.name}</span>
                  </div>
                  <div className="flex items-center gap-6">
                    <span className="text-xs text-white/40">{d.detections} detections</span>
                    <div className="flex items-center gap-2">
                      <div className="w-16 h-1.5 rounded-full bg-white/10 overflow-hidden">
                        <div className="h-full rounded-full bg-emerald-500" style={{ width: `${d.accuracy}%` }} />
                      </div>
                      <span className="text-[10px] text-emerald-400 font-mono">{d.accuracy}%</span>
                    </div>
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Recent Bans</h3>
            <div className="space-y-2">
              {bans.map((b) => (
                <div key={b.id} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
                  <div>
                    <p className="text-xs text-white/60">{b.player}</p>
                    <p className="text-[10px] text-white/30">{b.reason}</p>
                  </div>
                  <div className="text-right">
                    <span className={`text-[10px] px-1.5 py-0.5 rounded ${b.type === 'Permanent' ? 'bg-rose-500/20 text-rose-400' : 'bg-amber-500/20 text-amber-400'}`}>{b.type}</span>
                    <p className="text-[10px] text-white/20 mt-0.5">{b.issuedAt}</p>
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Quick Actions</h3>
            <div className="space-y-2">
              {[
                { label: 'Review Appeals', count: stats?.pendingAppeals ?? 0, onClick: () => navigate('/admin/appeals') },
                { label: 'Export Audit Log', count: null, onClick: handleExportAuditLog },
                { label: 'Update Blacklist', count: null, onClick: handleUpdateBlacklist },
                { label: 'System Config', count: null, onClick: () => navigate('/settings') },
              ].map((a) => (
                <button key={a.label} onClick={a.onClick} className="w-full flex items-center justify-between px-3 py-2.5 rounded-lg text-xs text-white/50 hover:text-white/80 hover:bg-white/5 transition-all">
                  {a.label}
                  {a.count != null && a.count > 0 && <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-primary-500/20 text-primary-300">{a.count}</span>}
                </button>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
