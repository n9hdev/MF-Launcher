import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Terminal, Activity, Shield, Cpu, Server, Wifi, Database, Cloud, ArrowRight, Power, RefreshCw, Users } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { StatusCard } from '../../components/ui/StatusCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { AnimatedModal } from '../../components/ui/AnimatedModal';
import { superAdminApi } from '../../services/superadmin';
import { useNotificationStore } from '../../stores/notificationStore';
import { useUIStore } from '../../stores/uiStore';
import type { ISuperAdminStats, IInfrastructureNode, ISystemHealth } from '../../services/superadmin';

export function CommandCenterPage() {
  const [stats, setStats] = useState<ISuperAdminStats | null>(null);
  const [nodes, setNodes] = useState<IInfrastructureNode[]>([]);
  const [health, setHealth] = useState<ISystemHealth | null>(null);
  const { addNotification } = useNotificationStore();
  const { addToast } = useUIStore();
  const [refreshing, setRefreshing] = useState(false);
  const [nodesModalOpen, setNodesModalOpen] = useState(false);
  const [consoleModalOpen, setConsoleModalOpen] = useState(false);

  const refresh = () => {
    setRefreshing(true);
    Promise.all([
      superAdminApi.getStats().then(({ data }) => setStats(data)),
      superAdminApi.getInfrastructureNodes().then(({ data }) => setNodes(data)),
      superAdminApi.getSystemHealth().then(({ data }) => setHealth(data)),
    ]).catch((err) => console.error('CommandCenterPage', err))
      .finally(() => setRefreshing(false));
  };

  const executeAction = (label: string) => {
    addNotification({
      id: `action-${Date.now()}`,
      type: 'info',
      title: label,
      message: `${label} command dispatched`,
      timestamp: 'Just now',
      read: false,
    });
    addToast({ type: 'success', title: label, message: `${label} initiated successfully` });
  };

  useEffect(() => {
    refresh();
  }, []);

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Command Center</h1>
          <p className="text-sm text-white/30 mt-0.5">Full infrastructure control and monitoring</p>
        </div>
        <div className="flex gap-2">
          <AnimatedButton size="sm" variant="secondary" icon={<RefreshCw size={12} />} loading={refreshing} onClick={refresh}>Refresh</AnimatedButton>
          <AnimatedButton size="sm" variant="gradient" icon={<Terminal size={12} />} onClick={() => setConsoleModalOpen(true)}>Open Console</AnimatedButton>
        </div>
      </motion.div>

      <div className="grid grid-cols-5 gap-4">
        <MetricCard title="Total Users" value={(stats?.totalUsers ?? '—').toLocaleString()} trend="up" subtitle="+2.1% this week" icon={<Users size={16} />} />
        <MetricCard title="Active Sessions" value={String(stats?.activeSessions ?? '—')} subtitle={`Peak: ${(stats?.activeSessions ?? 0) + 500}`} icon={<Activity size={16} />} />
        <MetricCard title="Detection Engine" value={`${stats?.detectionEngineUptime ?? '—'}%`} subtitle="Uptime: 30d" trend="up" icon={<Cpu size={16} />} />
        <MetricCard title="System Load" value={`${stats?.systemLoad ?? '—'}%`} subtitle={`Across ${nodes.length} nodes`} icon={<Server size={16} />} />
        <MetricCard title="Data Processed" value={stats?.dataProcessed ?? '—'} subtitle="This month" icon={<Database size={16} />} />
      </div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Infrastructure Nodes</h3>
              <button onClick={() => setNodesModalOpen(true)} className="text-[10px] text-primary-400 hover:text-primary-300 flex items-center gap-1">View all <ArrowRight size={10} /></button>
            </div>
            <div className="space-y-2">
              {nodes.map((node) => (
                <div key={node.name} className="flex items-center justify-between p-3 rounded-xl hover:bg-white/[0.03] transition-colors border border-transparent hover:border-white/5">
                  <div className="flex items-center gap-3">
                    <StatusCard title={node.name} status={node.status as 'active' | 'inactive' | 'error' | 'warning'} icon={<Server size={14} />} />
                  </div>
                  <div className="flex items-center gap-6">
                    <span className="text-[10px] text-white/30">{node.region}</span>
                    <span className="text-[10px] text-white/30">{node.uptime}</span>
                    <span className="text-[10px] text-white/30">Load: {node.load}</span>
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Quick Actions</h3>
            <div className="space-y-2">
              {[
                { label: 'Deploy Update', icon: <Cloud size={12} /> },
                { label: 'Restart Service', icon: <Power size={12} /> },
                { label: 'Clear Cache', icon: <RefreshCw size={12} /> },
                { label: 'Generate Report', icon: <Terminal size={12} /> },
                { label: 'Run Diagnostics', icon: <Activity size={12} /> },
              ].map((a) => (
                <button key={a.label} onClick={() => executeAction(a.label)} className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs text-white/50 hover:text-white/80 hover:bg-white/5 transition-all">
                  {a.icon}
                  {a.label}
                </button>
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">System Health</h3>
            <div className="space-y-2">
              {[
                { label: 'CPU', value: health?.cpu ?? 0, bar: health?.cpu ?? 0 },
                { label: 'Memory', value: health?.memory ?? 0, bar: health?.memory ?? 0 },
                { label: 'Disk', value: health?.disk ?? 0, bar: health?.disk ?? 0 },
                { label: 'Network', value: health?.network ?? 0, bar: health?.network ?? 0 },
              ].map((m) => (
                <div key={m.label} className="space-y-1">
                  <div className="flex items-center justify-between text-[10px]">
                    <span className="text-white/30">{m.label}</span>
                    <span className="text-white/50 font-mono">{m.value}%</span>
                  </div>
                  <div className="h-1 rounded-full bg-white/5 overflow-hidden">
                    <motion.div
                      initial={{ width: 0 }}
                      animate={{ width: `${m.bar}%` }}
                      transition={{ duration: 0.8 }}
                      className="h-full rounded-full bg-primary-500/60"
                    />
                  </div>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>

      <AnimatedModal open={nodesModalOpen} onClose={() => setNodesModalOpen(false)} title="All Infrastructure Nodes" width="max-w-2xl">
        <div className="space-y-2 max-h-96 overflow-y-auto">
          {nodes.map((node) => (
            <div key={node.name} className="flex items-center justify-between p-3 rounded-xl bg-white/[0.03] border border-white/5">
              <div className="flex items-center gap-3">
                <div className={`w-2 h-2 rounded-full ${node.status === 'active' ? 'bg-emerald-500' : node.status === 'warning' ? 'bg-amber-500' : 'bg-rose-500'}`} />
                <div>
                  <p className="text-sm text-white/70">{node.name}</p>
                  <p className="text-[10px] text-white/30">{node.region}</p>
                </div>
              </div>
              <div className="flex items-center gap-4">
                <span className="text-[10px] text-white/30">{node.uptime}</span>
                <span className="text-[10px] text-white/30">Load: {node.load}</span>
              </div>
            </div>
          ))}
        </div>
      </AnimatedModal>

      <AnimatedModal open={consoleModalOpen} onClose={() => setConsoleModalOpen(false)} title="Console" width="max-w-2xl">
        <div className="font-mono text-xs">
          <div className="bg-black/40 rounded-xl p-4 h-64 overflow-y-auto mb-4 space-y-1">
            <p className="text-emerald-400">[SYSTEM] Console initialized</p>
            <p className="text-white/30">[INFO] Connected to infrastructure nodes</p>
            <p className="text-white/30">[INFO] Detection engine running</p>
            <p className="text-white/20">Type a command below...</p>
          </div>
          <div className="flex gap-2">
            <input placeholder="Enter command..." className="flex-1 bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/30 font-mono" />
            <AnimatedButton variant="gradient" size="sm">Execute</AnimatedButton>
          </div>
        </div>
      </AnimatedModal>
    </div>
  );
}
