import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Flag, X } from 'lucide-react';
import { AnimatedModal } from '../ui/AnimatedModal';
import { AnimatedButton } from '../ui/AnimatedButton';
import { reportApi } from '../../services/reports';
import { useUIStore } from '../../stores/uiStore';

export function FlagPlayerButton() {
  const [open, setOpen] = useState(false);
  const [playerName, setPlayerName] = useState('');
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const { addToast } = useUIStore();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!playerName.trim() || !reason) return;
    setSubmitting(true);
    try {
      await reportApi.submitReport({
        ticketType: 'report_player',
        playerName: playerName.trim(),
        reason,
        description,
      });
      addToast({ type: 'success', title: 'Player Flagged', message: `${playerName} has been reported to moderators` });
      setOpen(false);
      setPlayerName('');
      setReason('');
      setDescription('');
    } catch {
      addToast({ type: 'error', title: 'Failed to flag player' });
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <motion.button
        whileHover={{ scale: 1.1 }}
        whileTap={{ scale: 0.9 }}
        onClick={() => setOpen(true)}
        className="fixed bottom-6 right-6 z-50 w-12 h-12 rounded-full bg-gradient-to-r from-primary-500 to-primary-700 text-white shadow-lg shadow-primary-500/30 flex items-center justify-center hover:shadow-primary-500/50 transition-shadow"
        title="Flag a player"
      >
        <Flag size={18} />
      </motion.button>

      <AnimatedModal open={open} onClose={() => setOpen(false)} title="Flag Player">
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Player Name *</label>
            <input value={playerName} onChange={(e) => setPlayerName(e.target.value)}
              className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40"
              placeholder="Enter player username" />
          </div>
          <div>
            <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Reason *</label>
            <select value={reason} onChange={(e) => setReason(e.target.value)}
              className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/60 outline-none focus:border-primary-500/40"
              style={{ colorScheme: 'dark' }}>
              <option value="" style={{ background: '#1e293b', color: '#94a3b8' }}>Select a reason...</option>
              <option value="Speed hack" style={{ background: '#1e293b', color: '#e2e8f0' }}>Speed hack</option>
              <option value="Aimbot / Wallhack" style={{ background: '#1e293b', color: '#e2e8f0' }}>Aimbot / Wallhack</option>
              <option value="Memory injection" style={{ background: '#1e293b', color: '#e2e8f0' }}>Memory injection</option>
              <option value="Script abuse" style={{ background: '#1e293b', color: '#e2e8f0' }}>Script abuse</option>
              <option value="Harassment" style={{ background: '#1e293b', color: '#e2e8f0' }}>Harassment</option>
              <option value="Other" style={{ background: '#1e293b', color: '#e2e8f0' }}>Other</option>
            </select>
          </div>
          <div>
            <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Description</label>
            <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={3}
              className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 resize-none"
              placeholder="Optional details..." />
          </div>
          <div className="flex gap-2 pt-2">
            <AnimatedButton variant="secondary" onClick={() => setOpen(false)} type="button" fullWidth>Cancel</AnimatedButton>
            <AnimatedButton variant="gradient" type="submit" disabled={!playerName.trim() || !reason} loading={submitting} fullWidth>Flag Player</AnimatedButton>
          </div>
        </form>
      </AnimatedModal>
    </>
  );
}