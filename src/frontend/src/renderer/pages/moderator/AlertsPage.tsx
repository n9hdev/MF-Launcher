import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { AlertTriangle, CheckCircle, Clock, Shield } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { ThreatCard } from '../../components/ui/ThreatCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { moderatorApi } from '../../services/moderator';
import type { IAlert } from '../../services/moderator';

export function AlertsPage() {
  const [alerts, setAlerts] = useState<IAlert[]>([]);

  useEffect(() => {
    const fetch = () => moderatorApi.getAlerts().then(({ data }) => setAlerts(data)).catch(() => {});
    fetch();
    const interval = setInterval(fetch, 15000);
    return () => clearInterval(interval);
  }, []);

  const critical = alerts.filter((a) => a.severity === 'critical').length;
  const high = alerts.filter((a) => a.severity === 'high').length;
  const medium = alerts.filter((a) => a.severity === 'medium').length;
  const resolved = alerts.filter((a) => a.resolved).length;

  const handleResolve = (id: string) => {
    moderatorApi.resolveAlert(id).then(() => {
      setAlerts((prev) => prev.map((a) => a.id === id ? { ...a, resolved: true } : a));
    }).catch((err) => console.error('[AlertsPage] failed to fetch', err));
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Security Alerts</h1>
          <p className="text-sm text-white/30 mt-0.5">Real-time threat monitoring & incident response</p>
        </div>
        <div className="flex items-center gap-2">
          <span className="text-[10px] px-2 py-1 rounded-full bg-rose-500/20 text-rose-400 border border-rose-500/30">{alerts.filter((a) => !a.resolved).length} Active</span>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Critical" value={String(critical)} subtitle="Immediate action needed" trend="up" icon={<AlertTriangle size={16} />} />
        <MetricCard title="High" value={String(high)} subtitle="Requires investigation" icon={<Shield size={16} />} />
        <MetricCard title="Medium" value={String(medium)} subtitle="Standard priority" icon={<Clock size={16} />} />
        <MetricCard title="Resolved" value={String(resolved)} subtitle="Addressed" trend="down" icon={<CheckCircle size={16} />} />
      </div>

      <GlassCard className="p-6">
        <div className="space-y-1">
          {alerts.map((alert) => (
            <ThreatCard
              key={alert.id}
              title={alert.title}
              description={alert.description}
              severity={alert.severity as 'low' | 'medium' | 'high' | 'critical'}
              confidence={alert.confidence}
              timestamp={alert.timestamp}
              processName={alert.processName}
              resolved={alert.resolved}
              onResolve={!alert.resolved ? () => handleResolve(alert.id) : undefined}
            />
          ))}
        </div>
      </GlassCard>
    </div>
  );
}
