import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Shield, RefreshCw, Power, Activity } from 'lucide-react';
import { useDetectionStore } from '../../stores/detectionStore';
import { detectionApi } from '../../services/detection';
import { historyApi } from '../../services/history';
import { useUIStore } from '../../stores/uiStore';
import type { IDetectionStats } from '../../services/history';
import { GlassCard } from '../../components/ui/GlassCard';
import { DetectorCard } from '../../components/ui/DetectorCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';

export function ProtectionPage() {
  const { status, updateStatus } = useDetectionStore();
  const [stats, setStats] = useState<IDetectionStats | null>(null);
  const [scanning, setScanning] = useState(false);
  const { addToast } = useUIStore();

  useEffect(() => {
    historyApi.getStats().then(({ data }) => setStats(data)).catch((err) => console.error('[ProtectionPage] failed to fetch', err));
  }, []);

  const handleRunScan = async () => {
    setScanning(true);
    try {
      await detectionApi.runScan();
      addToast({ type: 'success', title: 'Scan Complete', message: 'Full system scan completed' });
    } catch {
      addToast({ type: 'error', title: 'Scan Failed', message: 'Could not run full scan' });
    } finally {
      setScanning(false);
    }
  };

  const toggleDetector = (key: string) => {
    const newStatus = status[key as keyof typeof status] === 'active' ? 'inactive' as const : 'active' as const;
    updateStatus({ [key]: newStatus });
    detectionApi.toggleDetector(key, newStatus === 'active').catch(() => {});
    addToast({ type: 'info', title: 'Detector Toggled', message: `${key.replace(/([A-Z])/g, ' $1').trim()} ${newStatus === 'active' ? 'enabled' : 'disabled'}` });
  };

  const enableAll = () => {
    const allActive: Record<string, 'active'> = {};
    Object.keys(status).forEach((key) => { allActive[key] = 'active'; });
    updateStatus(allActive);
    Object.keys(status).forEach((key) => detectionApi.toggleDetector(key, true).catch(() => {}));
    addToast({ type: 'success', title: 'All Enabled', message: 'All detectors activated' });
  };

  const disableAll = () => {
    const allInactive: Record<string, 'inactive'> = {};
    Object.keys(status).forEach((key) => { allInactive[key] = 'inactive'; });
    updateStatus(allInactive);
    Object.keys(status).forEach((key) => detectionApi.toggleDetector(key, false).catch(() => {}));
    addToast({ type: 'info', title: 'All Disabled', message: 'All detectors deactivated' });
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Protection Status</h1>
          <p className="text-sm text-white/30 mt-0.5">Real-time monitoring of all security modules</p>
        </div>
        <AnimatedButton variant="gradient" icon={<RefreshCw size={14} />} loading={scanning} onClick={handleRunScan}>Run Full Scan</AnimatedButton>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        {[
          { label: 'Total Scans', value: stats?.totalScans.toLocaleString() ?? '...' },
          { label: 'Threats Found', value: stats?.threatsFound.toLocaleString() ?? '...' },
          { label: 'False Positives', value: stats?.falsePositives.toLocaleString() ?? '...' },
          { label: 'Uptime', value: stats ? `${stats.uptimePercent}%` : '...' },
        ].map((s) => (
          <GlassCard key={s.label} className="text-center py-4">
            <p className="text-lg font-bold text-white">{s.value}</p>
            <p className="text-[10px] text-white/30 mt-0.5">{s.label}</p>
          </GlassCard>
        ))}
      </div>

      <div className="grid grid-cols-2 gap-6">
        <GlassCard className="p-6">
          <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Detection Modules</h3>
          <div className="space-y-3">
            {Object.entries(status).map(([key, value]) => (
              <DetectorCard
                key={key}
                name={key.replace(/([A-Z])/g, ' $1').trim()}
                status={value}
                description={`Monitors ${key.replace(/([A-Z])/g, ' $1').toLowerCase()}`}
                detections={undefined}
                accuracy={undefined}
                onToggle={() => toggleDetector(key)}
              />
            ))}
          </div>
        </GlassCard>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Detection Stats</h3>
            <div className="space-y-4">
              {[
                { label: 'Clean Scans', value: stats?.cleanScans.toLocaleString() ?? '...', color: 'text-emerald-400' },
                { label: 'Threats Blocked', value: stats?.threatsFound.toLocaleString() ?? '...', color: 'text-rose-400' },
                { label: 'Accuracy Rate', value: stats ? `${((1 - stats.falsePositives / Math.max(stats.threatsFound, 1)) * 100).toFixed(1)}%` : '...', color: 'text-primary-400' },
              ].map((item) => (
                <div key={item.label} className="flex items-center justify-between py-2 border-b border-white/5 last:border-0">
                  <span className="text-xs text-white/50">{item.label}</span>
                  <span className={`text-xs font-mono ${item.color}`}>{item.value}</span>
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Module Controls</h3>
            <div className="flex gap-2">
              <AnimatedButton size="sm" variant="secondary" icon={<Power size={12} />} onClick={enableAll}>Enable All</AnimatedButton>
              <AnimatedButton size="sm" variant="ghost" icon={<Power size={12} />} onClick={disableAll}>Disable All</AnimatedButton>
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
