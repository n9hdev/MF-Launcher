import { motion } from 'framer-motion';
import { Shield, ShieldOff, AlertTriangle, Skull } from 'lucide-react';
import type { Severity } from '../../types/global';
import { AnimatedButton } from './AnimatedButton';

interface IThreatCardProps {
  title: string;
  description: string;
  severity: Severity;
  confidence: number;
  timestamp: string;
  processName?: string;
  resolved?: boolean;
  onResolve?: () => void;
  onInspect?: () => void;
}

const severityConfig = {
  low: { icon: Shield, color: 'text-primary-400', bg: 'bg-primary-500/10', border: 'border-primary-500/20', label: 'Low' },
  medium: { icon: AlertTriangle, color: 'text-amber-400', bg: 'bg-amber-500/10', border: 'border-amber-500/20', label: 'Medium' },
  high: { icon: ShieldOff, color: 'text-rose-400', bg: 'bg-rose-500/10', border: 'border-rose-500/20', label: 'High' },
  critical: { icon: Skull, color: 'text-red-400', bg: 'bg-red-500/10', border: 'border-red-500/20', label: 'Critical' },
};

export function ThreatCard({ title, description, severity, confidence, timestamp, processName, resolved, onResolve, onInspect }: IThreatCardProps) {
  const cfg = severityConfig[severity];

  return (
    <motion.div
      initial={{ opacity: 0, x: -20 }}
      animate={{ opacity: 1, x: 0 }}
      className={`glass rounded-xl p-4 border ${cfg.border} ${resolved ? 'opacity-50' : ''}`}
    >
      <div className="flex items-start gap-4">
        <div className={`w-10 h-10 rounded-xl ${cfg.bg} flex items-center justify-center flex-shrink-0 ${cfg.color}`}>
          <cfg.icon size={20} />
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-3">
            <div>
              <h4 className="text-sm font-semibold text-white/80">{title}</h4>
              <p className="text-xs text-white/40 mt-0.5">{description}</p>
            </div>
            <span className={`text-[10px] font-bold px-2 py-1 rounded-full ${cfg.bg} ${cfg.color} flex-shrink-0`}>
              {cfg.label}
            </span>
          </div>
          <div className="flex items-center gap-4 mt-3">
            <div className="flex items-center gap-1.5 text-[10px] text-white/30">
              <span>Confidence:</span>
              <div className="w-16 h-1.5 rounded-full bg-white/10 overflow-hidden">
                <div
                  className={`h-full rounded-full ${confidence > 80 ? 'bg-rose-500' : confidence > 50 ? 'bg-amber-500' : 'bg-primary-500'}`}
                  style={{ width: `${confidence}%` }}
                />
              </div>
              <span className="font-mono">{confidence}%</span>
            </div>
            {processName && <span className="text-[10px] text-white/20 font-mono">{processName}</span>}
            <span className="text-[10px] text-white/20 ml-auto">{timestamp}</span>
          </div>
          {!resolved && (onResolve || onInspect) && (
            <div className="flex gap-2 mt-3">
              {onInspect && <AnimatedButton size="sm" variant="secondary" onClick={onInspect}>Inspect</AnimatedButton>}
              {onResolve && <AnimatedButton size="sm" variant="ghost" onClick={onResolve}>Dismiss</AnimatedButton>}
            </div>
          )}
          {resolved && <p className="text-[10px] text-emerald-400/60 mt-2">Resolved</p>}
        </div>
      </div>
    </motion.div>
  );
}
