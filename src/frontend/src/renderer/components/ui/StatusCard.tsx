import { motion } from 'framer-motion';
import type { ModuleStatus } from '../../types/global';

interface IStatusCardProps {
  title: string;
  status: ModuleStatus;
  description?: string;
  icon?: React.ReactNode;
  onClick?: () => void;
}

const statusConfig = {
  active: { dot: 'bg-emerald-500', bg: 'bg-emerald-500/10', border: 'border-emerald-500/20', text: 'text-emerald-400', label: 'Active' },
  inactive: { dot: 'bg-white/20', bg: 'bg-white/5', border: 'border-white/5', text: 'text-white/30', label: 'Inactive' },
  error: { dot: 'bg-rose-500', bg: 'bg-rose-500/10', border: 'border-rose-500/20', text: 'text-rose-400', label: 'Error' },
  warning: { dot: 'bg-amber-500', bg: 'bg-amber-500/10', border: 'border-amber-500/20', text: 'text-amber-400', label: 'Warning' },
};

export function StatusCard({ title, status, description, icon, onClick }: IStatusCardProps) {
  const cfg = statusConfig[status];

  return (
    <motion.button
      whileHover={onClick ? { scale: 1.02, y: -2 } : undefined}
      onClick={onClick}
      className={`glass rounded-xl p-4 text-left w-full border ${cfg.border} ${onClick ? 'cursor-pointer' : ''}`}
    >
      <div className="flex items-start justify-between mb-3">
        <div className={`w-9 h-9 rounded-lg ${cfg.bg} flex items-center justify-center ${cfg.text}`}>
          {icon}
        </div>
        <span className={`inline-flex items-center gap-1.5 text-xs font-medium ${cfg.text}`}>
          <span className={`w-1.5 h-1.5 rounded-full ${cfg.dot} ${status === 'active' ? 'animate-pulse' : ''}`} />
          {cfg.label}
        </span>
      </div>
      <h4 className="text-sm font-semibold text-white/80 mb-0.5">{title}</h4>
      {description && <p className="text-xs text-white/40">{description}</p>}
    </motion.button>
  );
}
