import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Server, Cloud, Database, Wifi, HardDrive, Activity, AlertTriangle } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { StatusCard } from '../../components/ui/StatusCard';
import { superAdminApi } from '../../services/superadmin';
import type { IServerNode, IInfrastructureStats } from '../../services/superadmin';

export function InfrastructurePage() {
  const [servers, setServers] = useState<IServerNode[]>([]);
  const [infraStats, setInfraStats] = useState<IInfrastructureStats | null>(null);

  useEffect(() => {
    superAdminApi.getServers().then(({ data }) => setServers(data)).catch((err) => console.error('InfrastructurePage', err));
    superAdminApi.getInfrastructureStats().then(({ data }) => setInfraStats(data)).catch((err) => console.error('InfrastructurePage', err));
  }, []);

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Infrastructure</h1>
        <p className="text-sm text-white/30 mt-0.5">Server fleet monitoring and management</p>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Total Servers" value={String(infraStats?.totalServers ?? '—')} icon={<Server size={16} />} />
        <MetricCard title="Online" value={String(infraStats?.online ?? '—')} trend="up" icon={<Cloud size={16} />} />
        <MetricCard title="Avg CPU" value={infraStats ? `${infraStats.avgCpu}%` : '—'} icon={<HardDrive size={16} />} />
        <MetricCard title="Avg Memory" value={infraStats ? `${infraStats.avgMem}%` : '—'} icon={<Database size={16} />} />
      </div>

      <div className="grid grid-cols-2 gap-6">
        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Server Fleet</h3>
          <div className="space-y-2">
            {servers.map((s) => (
              <div key={s.name} className="flex items-center justify-between p-3 rounded-xl hover:bg-white/[0.03] transition-colors border border-transparent hover:border-white/5">
                <StatusCard title={s.name} status={s.status as 'active' | 'inactive' | 'error' | 'warning'} icon={<Server size={14} />} />
                <div className="flex items-center gap-4 text-[10px]">
                  <span className="text-white/30 w-20">{s.type}</span>
                  <span className="text-white/20 w-14">{s.region}</span>
                  <div className="flex items-center gap-2 w-16">
                    <div className="w-10 h-1 rounded-full bg-white/5 overflow-hidden">
                      <div className="h-full rounded-full bg-primary-500/60" style={{ width: `${s.cpu}%` }} />
                    </div>
                    <span className="text-white/30 font-mono">{s.cpu}%</span>
                  </div>
                </div>
              </div>
            ))}
          </div>
        </GlassCard>

        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Resource Distribution</h3>
          <div className="space-y-4">
            {[
              { label: 'CPU', value: infraStats?.avgCpu ?? 0, color: 'bg-primary-500' },
              { label: 'Memory', value: infraStats?.avgMem ?? 0, color: 'bg-violet-500' },
              { label: 'Disk', value: 39, color: 'bg-cyan-500' },
              { label: 'Network', value: 18, color: 'bg-emerald-500' },
            ].map((r) => (
              <div key={r.label} className="space-y-1">
                <div className="flex items-center justify-between text-xs">
                  <span className="text-white/40">{r.label}</span>
                  <span className="text-white/50 font-mono">{r.value}%</span>
                </div>
                <div className="h-2 rounded-full bg-white/5 overflow-hidden">
                  <motion.div
                    initial={{ width: 0 }}
                    animate={{ width: `${r.value}%` }}
                    transition={{ duration: 0.8 }}
                    className={`h-full rounded-full ${r.color}/60`}
                  />
                </div>
              </div>
            ))}
          </div>
        </GlassCard>
      </div>
    </div>
  );
}
