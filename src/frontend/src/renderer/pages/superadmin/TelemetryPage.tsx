import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { Activity, Cpu, HardDrive, Wifi, Zap, Thermometer } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { superAdminApi } from '../../services/superadmin';
import type { ITelemetryMetric, ISystemResource } from '../../services/superadmin';

export function TelemetryPage() {
  const [metrics, setMetrics] = useState<ITelemetryMetric[]>([]);
  const [resources, setResources] = useState<ISystemResource[]>([]);

  useEffect(() => {
    superAdminApi.getTelemetryMetrics().then(({ data }) => setMetrics(data)).catch((err) => console.error('TelemetryPage', err));
    superAdminApi.getSystemResources().then(({ data }) => setResources(data)).catch((err) => console.error('TelemetryPage', err));
  }, []);

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Telemetry</h1>
        <p className="text-sm text-white/30 mt-0.5">Real-time system performance metrics</p>
      </motion.div>

      <div className="grid grid-cols-3 gap-4">
        {metrics.map((m) => (
          <GlassCard key={m.label} className="py-4 px-5">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-[10px] text-white/30 uppercase tracking-wider">{m.label}</p>
                <p className="text-lg font-bold text-white mt-1">{m.value}</p>
              </div>
              <span className={`text-[10px] ${m.trend === 'up' ? 'text-emerald-400' : 'text-rose-400'}`}>{m.change}</span>
            </div>
          </GlassCard>
        ))}
      </div>

      <GlassCard className="p-6">
        <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-6">System Resources</h3>
        <div className="grid grid-cols-2 gap-6">
          {resources.map((r) => (
            <div key={r.label} className="space-y-2">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  <span className="text-primary-400">{r.label === 'CPU Usage' ? <Cpu size={16} /> : r.label === 'Memory' ? <HardDrive size={16} /> : r.label === 'Network Bandwidth' ? <Wifi size={16} /> : <Zap size={16} />}</span>
                  <span className="text-xs text-white/50">{r.label}</span>
                </div>
                <span className="text-xs font-mono text-white/60">{r.value}%</span>
              </div>
              <div className="h-2 rounded-full bg-white/5 overflow-hidden">
                <motion.div
                  initial={{ width: 0 }}
                  animate={{ width: `${r.value}%` }}
                  transition={{ duration: 1, ease: 'easeOut' }}
                  className={`h-full rounded-full ${r.color}/60`}
                />
              </div>
            </div>
          ))}
        </div>
      </GlassCard>
    </div>
  );
}
