import { motion } from 'framer-motion';
import { Power, Activity, AlertTriangle } from 'lucide-react';
import type { ModuleStatus } from '../../types/global';

interface IDetectorCardProps {
  name: string;
  status: ModuleStatus;
  description: string;
  detections?: number;
  accuracy?: string;
  onToggle?: () => void;
}

const statusStyles: Record<ModuleStatus, { dot: string; bg: string; text: string }> = {
  active: { dot: 'bg-emerald-500', bg: 'bg-emerald-500/10', text: 'text-emerald-400' },
  inactive: { dot: 'bg-white/20', bg: 'bg-white/5', text: 'text-white/30' },
  error: { dot: 'bg-rose-500', bg: 'bg-rose-500/10', text: 'text-rose-400' },
  warning: { dot: 'bg-amber-500', bg: 'bg-amber-500/10', text: 'text-amber-400' },
};

export function DetectorCard({ name, status, description, detections, accuracy, onToggle }: IDetectorCardProps) {
  const s = statusStyles[status];

  return (
    <motion.div
      whileHover={{ y: -2 }}
      className="glass rounded-xl p-4 border border-white/5"
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-3">
          <div className={`w-9 h-9 rounded-lg ${s.bg} flex items-center justify-center ${s.text}`}>
            <Activity size={16} />
          </div>
          <div>
            <h4 className="text-sm font-semibold text-white/80">{name}</h4>
            <p className="text-[10px] text-white/40">{description}</p>
          </div>
        </div>
        <motion.button
          whileHover={{ scale: 1.1 }}
          whileTap={{ scale: 0.9 }}
          onClick={onToggle}
          className={`w-8 h-8 rounded-lg flex items-center justify-center ${status === 'active' ? 'bg-emerald-500/10 text-emerald-400' : 'bg-white/5 text-white/20'}`}
        >
          <Power size={14} />
        </motion.button>
      </div>
      <div className="flex items-center justify-between pt-3 border-t border-white/5">
        <div className="flex items-center gap-1.5">
          <AlertTriangle size={10} className="text-white/20" />
          <span className="text-[11px] text-white/40">{detections ?? '—'} detections</span>
        </div>
        <span className="text-[11px] text-emerald-400/60">{accuracy ?? '—'} accuracy</span>
      </div>
    </motion.div>
  );
}
