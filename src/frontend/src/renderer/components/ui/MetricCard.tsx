import { motion } from 'framer-motion';
import { TrendingUp, TrendingDown, Minus } from 'lucide-react';

interface IMetricCardProps {
  title: string;
  value: string | number;
  subtitle?: string;
  trend?: 'up' | 'down' | 'neutral';
  trendValue?: string;
  icon?: React.ReactNode;
  className?: string;
  onClick?: () => void;
  loading?: boolean;
}

export function MetricCard({ title, value, subtitle, trend, trendValue, icon, className = '', onClick, loading }: IMetricCardProps) {
  if (loading) {
    return (
      <div className="glass rounded-xl p-5">
        <div className="skeleton h-3 w-20 mb-3" />
        <div className="skeleton h-8 w-32 mb-2" />
        <div className="skeleton h-3 w-24" />
      </div>
    );
  }

  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ type: 'spring', stiffness: 300, damping: 25 }}
      whileHover={onClick ? { scale: 1.02, y: -2 } : { y: -2 }}
      onClick={onClick}
      className={`glass rounded-xl p-5 ${onClick ? 'cursor-pointer' : ''} ${className}`}
    >
      <div className="flex items-start justify-between mb-3">
        <span className="text-[11px] text-white/40 uppercase tracking-wider font-semibold">{title}</span>
        {icon && <span className="text-primary-400">{icon}</span>}
      </div>
      <div className="flex items-end gap-2.5">
        <span className="text-2xl font-bold text-white tracking-tight">{value}</span>
        {trend && trend !== 'neutral' && (
          <span className={`flex items-center text-xs mb-1 ${
            trend === 'up' ? 'text-emerald-400' : 'text-rose-400'
          }`}>
            {trend === 'up' ? <TrendingUp size={14} className="mr-0.5" /> : <TrendingDown size={14} className="mr-0.5" />}
            {trendValue}
          </span>
        )}
        {trend === 'neutral' && (
          <span className="flex items-center text-xs mb-1 text-white/30">
            <Minus size={14} className="mr-0.5" />
            {trendValue}
          </span>
        )}
      </div>
      {subtitle && <p className="text-[11px] text-white/30 mt-1.5">{subtitle}</p>}
    </motion.div>
  );
}
