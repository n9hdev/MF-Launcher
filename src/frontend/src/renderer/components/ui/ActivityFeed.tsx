import { motion } from 'framer-motion';
import { Shield, UserCheck, AlertTriangle, Activity, Clock } from 'lucide-react';

interface IActivity {
  id: string;
  type: 'scan' | 'user' | 'alert' | 'system' | 'session';
  title: string;
  description: string;
  timestamp: string;
  icon?: React.ReactNode;
}

interface IActivityFeedProps {
  activities: IActivity[];
}

const typeIcons: Record<string, React.ReactNode> = {
  scan: <Shield size={14} />,
  user: <UserCheck size={14} />,
  alert: <AlertTriangle size={14} />,
  system: <Activity size={14} />,
  session: <Clock size={14} />,
};

export function ActivityFeed({ activities }: IActivityFeedProps) {
  return (
    <div className="space-y-1">
      {activities.map((a, i) => (
        <motion.div
          key={a.id}
          initial={{ opacity: 0, y: 8 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: i * 0.04 }}
          className="flex items-center gap-3 px-3 py-2.5 rounded-lg hover:bg-white/[0.03] transition-colors"
        >
          <div className="w-7 h-7 rounded-lg bg-white/5 flex items-center justify-center text-white/30 flex-shrink-0">
            {a.icon || typeIcons[a.type]}
          </div>
          <div className="flex-1 min-w-0">
            <p className="text-sm text-white/70 truncate">{a.title}</p>
            <p className="text-[10px] text-white/30 truncate">{a.description}</p>
          </div>
          <span className="text-[10px] text-white/20 flex-shrink-0">{a.timestamp}</span>
        </motion.div>
      ))}
    </div>
  );
}
