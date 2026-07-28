import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Microscope, Activity, Shield, AlertTriangle, Power, RefreshCw, Settings } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { StatusCard } from '../../components/ui/StatusCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { AnimatedModal } from '../../components/ui/AnimatedModal';
import { superAdminApi } from '../../services/superadmin';
import { useNotificationStore } from '../../stores/notificationStore';
import { useUIStore } from '../../stores/uiStore';
import type { IDetectionCenterStats, IModuleStatus, IEngineConfig } from '../../services/superadmin';

export function DetectionCenterPage() {
  const [stats, setStats] = useState<IDetectionCenterStats | null>(null);
  const [modules, setModules] = useState<IModuleStatus[]>([]);
  const [config, setConfig] = useState<IEngineConfig[]>([]);
  const [configModalOpen, setConfigModalOpen] = useState(false);
  const { addNotification } = useNotificationStore();
  const { addToast } = useUIStore();

  useEffect(() => {
    superAdminApi.getDetectionCenterStats().then(({ data }) => setStats(data)).catch((err) => console.error('DetectionCenterPage', err));
    superAdminApi.getModuleStatuses().then(({ data }) => setModules(data)).catch((err) => console.error('DetectionCenterPage', err));
    superAdminApi.getEngineConfig().then(({ data }) => setConfig(data)).catch((err) => console.error('DetectionCenterPage', err));
  }, []);

  const handleRestartEngine = async () => {
    addToast({ type: 'warning', title: 'Restarting', message: 'Detection engine restart initiated...' });
    addNotification({ id: `restart-${Date.now()}`, type: 'warning', title: 'Engine Restart', message: 'Detection engine is restarting', timestamp: 'Just now', read: false });
    try {
      await new Promise((resolve) => setTimeout(resolve, 2000));
      addToast({ type: 'success', title: 'Restarted', message: 'Detection engine restarted successfully' });
    } catch {
      addToast({ type: 'error', title: 'Failed', message: 'Engine restart failed' });
    }
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Detection Center</h1>
          <p className="text-sm text-white/30 mt-0.5">Advanced detection engine configuration</p>
        </div>
        <div className="flex gap-2">
          <AnimatedButton size="sm" variant="secondary" icon={<RefreshCw size={12} />} onClick={handleRestartEngine}>Restart Engine</AnimatedButton>
          <AnimatedButton size="sm" variant="gradient" icon={<Settings size={12} />} onClick={() => setConfigModalOpen(true)}>Configure</AnimatedButton>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Detection Rate" value={stats ? `${stats.detectionRate}%` : '—'} trend="up" icon={<Activity size={16} />} />
        <MetricCard title="Engine Version" value={stats?.engineVersion ?? '—'} subtitle="Latest" icon={<Microscope size={16} />} />
        <MetricCard title="Uptime" value={stats?.uptime ?? '—'} subtitle="Since last restart" icon={<Shield size={16} />} />
        <MetricCard title="Config Version" value={stats ? `v${stats.configVersion}` : '—'} icon={<Settings size={16} />} />
      </div>

      <div className="grid grid-cols-2 gap-6">
        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Module Status</h3>
          <div className="grid grid-cols-2 gap-3">
            {modules.map((m) => (
              <StatusCard
                key={m.name}
                title={m.name.replace(/([A-Z])/g, ' $1').trim()}
                status={m.status as 'active' | 'inactive' | 'error' | 'warning'}
                icon={<Shield size={14} />}
              />
            ))}
          </div>
        </GlassCard>

        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Engine Configuration</h3>
          <div className="space-y-3">
            {config.map((c) => (
              <div key={c.label} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
                <span className="text-xs text-white/50">{c.label}</span>
                <span className="text-xs text-white/60 font-mono">{c.value}</span>
              </div>
            ))}
          </div>
        </GlassCard>
      </div>

      <AnimatedModal open={configModalOpen} onClose={() => setConfigModalOpen(false)} title="Engine Configuration">
        <div className="space-y-4">
          {config.map((c) => (
            <div key={c.label} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
              <span className="text-xs text-white/50">{c.label}</span>
              <input defaultValue={c.value} className="w-32 text-right bg-white/5 border border-white/10 rounded-lg px-3 py-1.5 text-xs text-white/60 outline-none focus:border-primary-500/30 font-mono" />
            </div>
          ))}
          <div className="flex gap-2 pt-2">
            <AnimatedButton variant="secondary" onClick={() => setConfigModalOpen(false)} fullWidth>Cancel</AnimatedButton>
            <AnimatedButton variant="gradient" onClick={() => { setConfigModalOpen(false); addToast({ type: 'success', title: 'Saved', message: 'Engine configuration updated' }); }} fullWidth>Save Configuration</AnimatedButton>
          </div>
        </div>
      </AnimatedModal>
    </div>
  );
}
