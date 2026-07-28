import { useState, useEffect } from 'react';
import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import { Shield, AlertTriangle, Clock, UserX, Mail, Send, LogOut, ExternalLink, MessageSquare } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { useAuthStore } from '../../stores/authStore';
import { usePermissionStore } from '../../stores/permissionStore';
import { authApi } from '../../services/auth';
import { disconnectSignalR } from '../../services/signalr';

export function BannedPage() {
  const navigate = useNavigate();
  const { banInfo, user, logout, sessionId, setBanned } = useAuthStore();
  const [appealMessage, setAppealMessage] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [banId, setBanId] = useState(banInfo?.id || '');

  useEffect(() => {
    if (banInfo?.id) {
      setBanId(banInfo.id);
      return;
    }
    authApi.getActiveBan().then(({ data }) => {
      const d = data as { banned: boolean; ban?: { id?: string; reason?: string; type?: string; issuedBy?: string; issuedAt?: string; proofUrl?: string; durationHours?: number; bannedAt?: string } };
      if (d.banned && d.ban?.id) {
        setBanId(d.ban.id);
        setBanned(true, {
          id: d.ban.id || '',
          reason: d.ban.reason || 'Your account has been banned',
          type: d.ban.type || 'Permanent',
          issuedBy: d.ban.issuedBy || 'System',
          issuedAt: d.ban.issuedAt || new Date().toISOString(),
          proofUrl: d.ban.proofUrl || undefined,
          durationHours: d.ban.durationHours || 0,
          bannedAt: d.ban.bannedAt || new Date().toISOString(),
        });
      }
    }).catch(() => {});
  }, [banInfo?.id, setBanned]);

  const handleSubmitAppeal = async () => {
    if (!appealMessage.trim()) return;
    if (!banId) {
      setSubmitError('Ban information not available. Please refresh the page.');
      return;
    }
    setSubmitting(true);
    setSubmitError(null);
    try {
      await authApi.submitAppeal(banId, appealMessage);
      setSubmitted(true);
    } catch {
      setSubmitError('Failed to submit appeal. Please try again later.');
    } finally {
      setSubmitting(false);
    }
  };

  const handleLogout = async () => {
    try {
      if (sessionId) await authApi.logout(sessionId);
    } catch { /* ignore */ }
    await disconnectSignalR();
    usePermissionStore.getState().reset();
    logout();
  };

  const isPermanent = banInfo?.type?.toLowerCase() === 'permanent' || banInfo?.durationHours === -1;
  const durationText = isPermanent ? 'Permanent' : banInfo?.durationHours ? `${banInfo.durationHours}h` : 'Unknown';

  return (
    <div className="min-h-full flex items-center justify-center relative">
      <div className="absolute inset-0 bg-gradient-to-br from-rose-950/30 via-surface-900 to-surface-950" />
      <div className="absolute top-1/4 left-1/2 -translate-x-1/2 w-96 h-96 bg-rose-500/10 rounded-full blur-[120px]" />

      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        className="relative z-10 w-full max-w-lg px-4"
      >
        <GlassCard className="p-8 text-center">
          <motion.div
            animate={{ scale: [1, 1.05, 1] }}
            transition={{ duration: 3, repeat: Infinity }}
            className="w-20 h-20 rounded-2xl bg-gradient-to-br from-rose-500 to-rose-700 flex items-center justify-center mx-auto mb-6 shadow-lg shadow-rose-500/25"
          >
            <Shield size={40} className="text-white" />
          </motion.div>

          <h1 className="text-2xl font-extrabold text-white mb-2">Account Restricted</h1>
          <p className="text-sm text-white/40 mb-6">Your account has been suspended from the anti-cheat platform</p>

          <div className="space-y-3 text-left mb-6">
            <div className="flex items-center gap-3 px-4 py-3 rounded-xl bg-rose-500/5 border border-rose-500/10">
              <UserX size={16} className="text-rose-400 flex-shrink-0" />
              <div>
                <p className="text-[10px] text-white/30 uppercase tracking-wider">Account</p>
                <p className="text-sm text-white/70">{user?.username}</p>
              </div>
            </div>
            <div className="flex items-center gap-3 px-4 py-3 rounded-xl bg-rose-500/5 border border-rose-500/10">
              <AlertTriangle size={16} className="text-rose-400 flex-shrink-0" />
              <div>
                <p className="text-[10px] text-white/30 uppercase tracking-wider">Reason</p>
                <p className="text-sm text-white/70">{banInfo?.reason || 'No reason provided'}</p>
              </div>
            </div>
            <div className="flex items-center gap-3 px-4 py-3 rounded-xl bg-rose-500/5 border border-rose-500/10">
              <Clock size={16} className="text-rose-400 flex-shrink-0" />
              <div>
                <p className="text-[10px] text-white/30 uppercase tracking-wider">Duration</p>
                <p className="text-sm text-white/70">{durationText}</p>
              </div>
            </div>
            {banInfo?.issuedBy && (
              <div className="flex items-center gap-3 px-4 py-3 rounded-xl bg-rose-500/5 border border-rose-500/10">
                <Mail size={16} className="text-rose-400 flex-shrink-0" />
                <div>
                  <p className="text-[10px] text-white/30 uppercase tracking-wider">Issued By</p>
                  <p className="text-sm text-white/70">{banInfo.issuedBy}</p>
                </div>
              </div>
            )}
            {banInfo?.bannedAt && (
              <div className="flex items-center gap-3 px-4 py-3 rounded-xl bg-rose-500/5 border border-rose-500/10">
                <Clock size={16} className="text-rose-400 flex-shrink-0" />
                <div>
                  <p className="text-[10px] text-white/30 uppercase tracking-wider">Banned At</p>
                  <p className="text-sm text-white/70">{new Date(banInfo.bannedAt).toLocaleString()}</p>
                </div>
              </div>
            )}
            {banInfo?.proofUrl && (
              <a
                href={banInfo.proofUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-3 px-4 py-3 rounded-xl bg-primary-500/5 border border-primary-500/10 hover:bg-primary-500/10 transition-colors"
              >
                <ExternalLink size={16} className="text-primary-400 flex-shrink-0" />
                <div>
                  <p className="text-[10px] text-white/30 uppercase tracking-wider">Evidence</p>
                  <p className="text-sm text-primary-400">View proof</p>
                </div>
              </a>
            )}
          </div>

          {!submitted ? (
            <div className="text-left mb-4">
              <label className="block text-xs font-semibold text-white/50 uppercase tracking-wider mb-2">
                Submit an Appeal
              </label>
              <textarea
                value={appealMessage}
                onChange={(e) => setAppealMessage(e.target.value)}
                placeholder="Explain why this ban should be reviewed..."
                rows={4}
                className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-3 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 focus:ring-1 focus:ring-primary-500/20 transition-all resize-none mb-3"
              />
              {submitError && (
                <p className="text-xs text-rose-400 mb-3">{submitError}</p>
              )}
              <AnimatedButton
                variant="primary"
                fullWidth
                onClick={handleSubmitAppeal}
                loading={submitting}
                disabled={!appealMessage.trim()}
                icon={<Send size={14} />}
              >
                Submit Appeal
              </AnimatedButton>
            </div>
          ) : (
            <motion.div
              initial={{ opacity: 0, scale: 0.95 }}
              animate={{ opacity: 1, scale: 1 }}
              className="p-4 rounded-xl bg-emerald-500/10 border border-emerald-500/20 text-center mb-4"
            >
              <p className="text-sm text-emerald-400 font-medium">Appeal submitted successfully</p>
              <p className="text-xs text-white/30 mt-1">A moderator will review your case</p>
              <AnimatedButton
                variant="primary"
                size="sm"
                fullWidth
                onClick={() => navigate('/player/appeal')}
                icon={<MessageSquare size={12} />}
                className="mt-3"
              >
                View Appeal Ticket
              </AnimatedButton>
            </motion.div>
          )}

          <div className="pt-4 border-t border-white/5 space-y-2">
            <p className="text-xs text-white/30 text-center">
              For urgent inquiries, contact support at{' '}
              <a href="mailto:support@mafia-city.com" className="text-primary-400 hover:text-primary-300">
                support@mafia-city.com
              </a>
            </p>
            <AnimatedButton
              variant="ghost"
              size="sm"
              fullWidth
              onClick={handleLogout}
              icon={<LogOut size={12} />}
            >
              Sign Out
            </AnimatedButton>
          </div>
        </GlassCard>
      </motion.div>
    </div>
  );
}