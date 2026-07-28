import { motion } from 'framer-motion';
import { MoreVertical, Shield, AlertTriangle, CheckCircle } from 'lucide-react';
import type { IPlayer } from '../../types/global';

interface IUserCardProps {
  player: IPlayer;
  onClick?: () => void;
  onAction?: () => void;
}

export function UserCard({ player, onClick, onAction }: IUserCardProps) {
  return (
    <motion.div
      whileHover={{ y: -2, scale: 1.01 }}
      onClick={onClick}
      className="glass rounded-xl p-4 border border-white/5 cursor-pointer group"
    >
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <div className="relative">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center text-sm font-bold text-white">
              {player.username.charAt(0)}
            </div>
            <div className={`absolute -bottom-0.5 -right-0.5 w-3.5 h-3.5 rounded-full border-2 border-surface-900 ${
              player.status === 'online' ? 'bg-emerald-500' : player.status === 'suspected' ? 'bg-amber-500' : 'bg-white/20'
            }`} />
          </div>
          <div>
            <h4 className="text-sm font-semibold text-white/80">{player.username}</h4>
            <p className="text-[10px] text-white/30">{player.gameName} &middot; {player.hoursPlayed}h played</p>
          </div>
        </div>
        <button onClick={(e) => { e.stopPropagation(); onAction?.(); }} className="opacity-0 group-hover:opacity-100 text-white/30 hover:text-white/60 transition-all">
          <MoreVertical size={14} />
        </button>
      </div>

      <div className="flex items-center gap-4 mt-3 pt-3 border-t border-white/5">
        <div className="flex items-center gap-1.5">
          <Shield size={12} className={player.trustScore > 70 ? 'text-emerald-400' : player.trustScore > 30 ? 'text-amber-400' : 'text-rose-400'} />
          <span className="text-xs font-mono text-white/50">{player.trustScore}%</span>
        </div>
        <div className="flex items-center gap-1.5">
          <AlertTriangle size={12} className="text-amber-400/60" />
          <span className="text-xs text-white/40">{player.reportsCount} reports</span>
        </div>
        <span className="text-[10px] text-white/20 ml-auto">{player.lastSeen}</span>
      </div>
    </motion.div>
  );
}
