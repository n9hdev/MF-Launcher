import { motion, AnimatePresence } from 'framer-motion';
import { Bell, CheckCheck, X } from 'lucide-react';
import { useNotificationStore } from '../../stores/notificationStore';

export function NotificationCenter() {
  const { notifications, unreadCount, markRead, markAllRead, removeNotification } = useNotificationStore();

  return (
    <div className="glass rounded-xl overflow-hidden border border-white/5">
      <div className="flex items-center justify-between px-4 py-3 border-b border-white/5">
        <div className="flex items-center gap-2">
          <Bell size={14} className="text-primary-400" />
          <span className="text-sm font-semibold text-white/80">Notifications</span>
          {unreadCount > 0 && (
            <span className="text-[10px] px-1.5 py-0.5 rounded-full bg-primary-500/20 text-primary-300">{unreadCount}</span>
          )}
        </div>
        <button onClick={markAllRead} className="flex items-center gap-1 text-[10px] text-primary-400 hover:text-primary-300">
          <CheckCheck size={12} /> Mark all read
        </button>
      </div>
      <div className="max-h-96 overflow-y-auto">
        {notifications.length === 0 && (
          <p className="text-sm text-white/30 text-center py-8">No notifications</p>
        )}
        {notifications.map((n) => (
          <motion.div
            key={n.id}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className={`flex items-start gap-3 px-4 py-3 border-b border-white/5 last:border-0 group transition-colors ${
              n.read ? 'opacity-50' : 'bg-primary-500/[0.03]'
            }`}
          >
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2">
                <p className="text-xs font-medium text-white/80">{n.title}</p>
                {!n.read && <span className="w-1.5 h-1.5 rounded-full bg-primary-400 flex-shrink-0" />}
              </div>
              <p className="text-[10px] text-white/40 mt-0.5 truncate">{n.message}</p>
              <span className="text-[9px] text-white/20 mt-1 block">{n.timestamp}</span>
            </div>
            <div className="flex gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
              {!n.read && (
                <button onClick={() => markRead(n.id)} className="w-6 h-6 flex items-center justify-center rounded hover:bg-white/5 text-white/30 hover:text-white/60">
                  <CheckCheck size={12} />
                </button>
              )}
              <button onClick={() => removeNotification(n.id)} className="w-6 h-6 flex items-center justify-center rounded hover:bg-white/5 text-white/30 hover:text-white/60">
                <X size={12} />
              </button>
            </div>
          </motion.div>
        ))}
      </div>
    </div>
  );
}
