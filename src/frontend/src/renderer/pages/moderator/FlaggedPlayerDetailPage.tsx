import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, Flag, Shield, AlertTriangle, Clock, CheckCircle, XCircle, Camera, Video, FlagOff } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { ReportChatPanel } from '../../components/chat/ReportChatPanel';
import { moderatorApi } from '../../services/moderator';
import type { IPlayerReport, IReportMessage } from '../../services/reports';
import { useUIStore } from '../../stores/uiStore';
import { useAuthStore } from '../../stores/authStore';

const statusColor = (status: string) => {
  const s = status?.toLowerCase();
  const map: Record<string, string> = {
    pending: 'bg-primary-500/20 text-primary-300',
    open: 'bg-primary-500/20 text-primary-300',
    investigating: 'bg-amber-500/20 text-amber-400',
    inprogress: 'bg-amber-500/20 text-amber-400',
    resolved: 'bg-emerald-500/20 text-emerald-400',
    dismissed: 'bg-white/10 text-white/40',
  };
  return map[s] || 'bg-white/10 text-white/40';
};

const statusLabel = (status: string) => {
  const s = status?.toLowerCase();
  if (s === 'pending' || s === 'open') return 'Open';
  if (s === 'investigating' || s === 'inprogress') return 'In Progress';
  return status?.charAt(0).toUpperCase() + status?.slice(1);
};

export function FlaggedPlayerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const { addToast } = useUIStore();
  const [report, setReport] = useState<IPlayerReport | null>(null);
  const [messages, setMessages] = useState<IReportMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [chatEnabled, setChatEnabled] = useState(false);

  const fetchReport = useCallback(async () => {
    if (!id) return;
    try {
      const { data } = await moderatorApi.getPlayerReport(id);
      setReport(data);
      setChatEnabled(data.chatEnabled ?? false);
    } catch {
      addToast({ type: 'error', title: 'Failed to load report' });
    } finally {
      setLoading(false);
    }
  }, [id]);

  const fetchMessages = useCallback(async () => {
    if (!id) return;
    try {
      const { data } = await moderatorApi.getPlayerReportMessages(id);
      setMessages(data.messages);
    } catch {
      setMessages([]);
    }
  }, [id]);

  useEffect(() => {
    fetchReport();
    fetchMessages();
  }, [fetchReport, fetchMessages]);

  useEffect(() => {
    if (!id) return;
    const interval = setInterval(() => {
      moderatorApi.getPlayerReport(id).then(({ data }) => {
        setReport(data);
        setChatEnabled(data.chatEnabled ?? false);
      }).catch(() => {});
    }, 5000);
    return () => clearInterval(interval);
  }, [id]);

  const handleStatusChange = async (newStatus: string) => {
    if (!id) return;
    setActionLoading(true);
    try {
      await moderatorApi.updatePlayerReportStatus(id, newStatus);
      setReport((prev) => prev ? { ...prev, status: newStatus } : prev);
      addToast({ type: 'success', title: 'Status Updated', message: `Report marked as ${statusLabel(newStatus)}` });
    } catch {
      addToast({ type: 'error', title: 'Failed to update status' });
    } finally {
      setActionLoading(false);
    }
  };

  const handleToggleChat = async (enabled: boolean) => {
    if (!id) return;
    try {
      await moderatorApi.togglePlayerReportChat(id, enabled);
      setChatEnabled(enabled);
      setReport((prev) => prev ? { ...prev, chatEnabled: enabled } : prev);
      addToast({ type: 'success', title: enabled ? 'Chat Enabled' : 'Chat Disabled' });
    } catch {
      addToast({ type: 'error', title: 'Failed to toggle chat' });
    }
  };

  const handleSendMessage = async (message: string) => {
    if (!id) return;
    try {
      const { data } = await moderatorApi.sendPlayerReportMessage(id, message);
      setMessages((prev) => [...prev, data.message]);
    } catch {
      addToast({ type: 'error', title: 'Failed to send message' });
    }
  };

  const handleSendAttachment = async (file: File) => {
    if (!id) return;
    try {
      const { data } = await moderatorApi.sendPlayerReportAttachment(id, file);
      setMessages((prev) => [...prev, data.message]);
    } catch {
      addToast({ type: 'error', title: 'Failed to send attachment' });
    }
  };

  const handleUnflag = async () => {
    if (!id) return;
    setActionLoading(true);
    try {
      await moderatorApi.flagPlayerReport(id, false);
      setReport((prev) => prev ? { ...prev, isFlagged: false } : prev);
      addToast({ type: 'success', title: 'Unflagged', message: 'Report removed from flagged list' });
      navigate('/moderator/flagged');
    } catch {
      addToast({ type: 'error', title: 'Failed to unflag report' });
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-white/5 rounded-lg animate-pulse" />
        <div className="h-48 bg-white/5 rounded-xl animate-pulse" />
        <div className="h-64 bg-white/5 rounded-xl animate-pulse" />
      </div>
    );
  }

  if (!report) {
    return (
      <div className="flex flex-col items-center justify-center py-24">
        <AlertTriangle size={48} className="text-white/10 mb-4" />
        <p className="text-white/30 text-sm">Report not found</p>
        <AnimatedButton variant="secondary" onClick={() => navigate('/moderator/flagged')} className="mt-4">
          Back to Flagged Players
        </AnimatedButton>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-6xl">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <button
          onClick={() => navigate('/moderator/flagged')}
          className="flex items-center gap-1.5 text-xs text-white/30 hover:text-white/60 mb-4 transition-all"
        >
          <ArrowLeft size={14} /> Back to Flagged Players
        </button>

        <div className="flex items-center justify-between">
          <div className="flex items-center gap-4">
            <div className="w-12 h-12 rounded-2xl bg-gradient-to-br from-rose-500/20 to-primary-500/20 flex items-center justify-center">
              <Flag size={20} className="text-rose-400" />
            </div>
            <div>
              <div className="flex items-center gap-3">
                <h1 className="text-xl font-bold text-white">{report.playerName}</h1>
                <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${statusColor(report.status)}`}>
                  {statusLabel(report.status)}
                </span>
              </div>
              <p className="text-sm text-white/30 mt-0.5">{report.reason}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <span className="text-xs text-white/30">
              <Clock size={12} className="inline mr-1" />
              {new Date(report.createdAt).toLocaleDateString()}
            </span>
          </div>
        </div>
      </motion.div>

      <div className="flex gap-3">
        <AnimatedButton
          variant="secondary"
          icon={<FlagOff size={12} />}
          disabled={actionLoading}
          onClick={handleUnflag}
        >
          Unflag &amp; Remove
        </AnimatedButton>
        {(report.status?.toLowerCase() === 'pending' || report.status?.toLowerCase() === 'open') && (
          <>
            <AnimatedButton
              variant="secondary"
              icon={<Shield size={12} />}
              disabled={actionLoading}
              onClick={() => handleStatusChange('investigating')}
            >
              Start Investigation
            </AnimatedButton>
            <AnimatedButton
              variant="secondary"
              icon={<CheckCircle size={12} />}
              disabled={actionLoading}
              onClick={() => handleStatusChange('resolved')}
            >
              Resolve
            </AnimatedButton>
            <AnimatedButton
              variant="secondary"
              icon={<XCircle size={12} />}
              disabled={actionLoading}
              onClick={() => handleStatusChange('dismissed')}
            >
              Dismiss
            </AnimatedButton>
          </>
        )}
        {report.status?.toLowerCase() === 'investigating' && (
          <>
            <AnimatedButton
              variant="secondary"
              icon={<CheckCircle size={12} />}
              disabled={actionLoading}
              onClick={() => handleStatusChange('resolved')}
            >
              Resolve
            </AnimatedButton>
            <AnimatedButton
              variant="secondary"
              icon={<XCircle size={12} />}
              disabled={actionLoading}
              onClick={() => handleStatusChange('dismissed')}
            >
              Dismiss
            </AnimatedButton>
          </>
        )}
      </div>

      <div className="grid grid-cols-2 gap-6">
        <div className="space-y-6">
          {report.description && (
            <GlassCard className="p-6">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Description</h3>
              <p className="text-sm text-white/60 whitespace-pre-wrap">{report.description}</p>
              {report.attachmentUrl && (
                <div className="mt-3">
                  <a
                    href={report.attachmentUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 text-xs text-primary-400 hover:text-primary-300"
                  >
                    View Attachment
                  </a>
                </div>
              )}
            </GlassCard>
          )}

          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Quick Actions</h3>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <AnimatedButton
                variant="secondary"
                icon={<Camera size={12} />}
                onClick={() => navigate(`/admin/live-view?player=${encodeURIComponent(report.playerName)}`)}
              >
                Take Screenshot
              </AnimatedButton>
              <AnimatedButton
                variant="secondary"
                icon={<Video size={12} />}
                onClick={() => navigate(`/admin/live-view?player=${encodeURIComponent(report.playerName)}`)}
              >
                Start Stream
              </AnimatedButton>
            </div>
          </GlassCard>
        </div>

        <GlassCard className="p-0 overflow-hidden h-[500px]">
          <ReportChatPanel
            reportId={report.id}
            messages={messages}
            currentUserId={user?.id || ''}
            chatEnabled={chatEnabled}
            onToggleChat={handleToggleChat}
            canToggleChat={true}
            onSendMessage={handleSendMessage}
            onSendAttachment={handleSendAttachment}
          />
        </GlassCard>
      </div>
    </div>
  );
}