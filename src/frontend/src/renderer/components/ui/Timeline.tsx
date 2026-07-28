import { motion } from 'framer-motion';
import { CheckCircle, AlertTriangle, XCircle, Info, Shield } from 'lucide-react';
import type { Severity } from '../../types/global';

interface ITimelineEvent {
  id: string;
  type: 'success' | 'warning' | 'error' | 'info' | 'achievement';
  title: string;
  description?: string;
  timestamp: string;
  severity?: Severity;
  count?: number;
}

interface ITimelineProps {
  events: ITimelineEvent[];
}

const iconMap = {
  success: CheckCircle, warning: AlertTriangle, error: XCircle, info: Info, achievement: Shield,
};

const colorMap = {
  success: 'text-emerald-400 border-emerald-500/30',
  warning: 'text-amber-400 border-amber-500/30',
  error: 'text-rose-400 border-rose-500/30',
  info: 'text-primary-400 border-primary-500/30',
  achievement: 'text-violet-400 border-violet-500/30',
};

export function Timeline({ events }: ITimelineProps) {
  return (
    <div className="space-y-0">
      {events.map((event, i) => {
        const Icon = iconMap[event.type];
        return (
          <motion.div
            key={event.id}
            initial={{ opacity: 0, x: -10 }}
            animate={{ opacity: 1, x: 0 }}
            transition={{ delay: i * 0.05 }}
            className="relative flex gap-4 pb-6 last:pb-0"
          >
            <div className="flex flex-col items-center">
              <div className={`w-8 h-8 rounded-full ${colorMap[event.type]} bg-white/5 flex items-center justify-center border`}>
                <Icon size={14} />
              </div>
              {i < events.length - 1 && <div className="w-px flex-1 bg-white/5 mt-1" />}
            </div>
              <div className="flex-1 pb-2">
              <div className="flex items-start justify-between">
                <div className="flex items-center gap-2">
                  <p className="text-sm font-medium text-white/80">{event.title}</p>
                  {event.count && event.count > 1 && (
                    <span className="text-[10px] font-mono px-1.5 py-0.5 rounded bg-white/10 text-white/40">{event.count}x</span>
                  )}
                </div>
                <span className="text-[10px] text-white/20 flex-shrink-0 ml-2">{event.timestamp}</span>
              </div>
              {event.description && <p className="text-xs text-white/40 mt-0.5">{event.description}</p>}
            </div>
          </motion.div>
        );
      })}
    </div>
  );
}
