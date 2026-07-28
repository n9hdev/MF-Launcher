import { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, Flag, Bug, HelpCircle, MessageSquare, AlertTriangle, FlagOff } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { Select } from '../../components/ui/Select';
import { ReportChatPanel } from '../../components/chat/ReportChatPanel';
import { moderatorApi } from '../../services/moderator';
import type { IPlayerReport, IReportMessage } from '../../services/reports';
import { useAuthStore } from '../../stores/authStore';
import { useUIStore } from '../../stores/uiStore';

const statusOptions = ['pending', 'investigating', 'resolved', 'dismissed'];
const statusColors: Record<string, string> = {
  pending: 'bg-amber-500/20 text-amber-400',
  investigating: 'bg-primary-500/20 text-primary-300',
  resolved: 'bg-emerald-500/20 text-emerald-400',
  dismissed: 'bg-white/5 text-white/30',
};

export function ReportDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const { addToast } = useUIStore();
  const [report, setReport] = useState<IPlayerReport | null>(null);
  const [messages, setMessages] = useState<IReportMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [flagLoading, setFlagLoading] = useState(false);

  useEffect(() => {
    if (!id) return;
    (async () => {
      try {
        const { data: reportData } = await moderatorApi.getPlayerReport(id);
        setReport(reportData);
        setMessages(reportData.messages || []);
      } catch {
        setError('Failed to load report');
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  const handleSendMessage = async (message: string) => {
    if (!id) return;
    const { data } = await moderatorApi.sendPlayerReportMessage(id, message);
    setMessages((prev) => [...prev, data.message]);
  };

  const handleSendAttachment = async (file: File) => {
    if (!id) return;
    const { data } = await moderatorApi.sendPlayerReportAttachment(id, file);
    setMessages((prev) => [...prev, data.message]);
  };

  const handleToggleChat = async (enabled: boolean) => {
    if (!id || !report) return;
    try {
      await moderatorApi.togglePlayerReportChat(id, enabled);
      setReport({ ...report, chatEnabled: enabled });
      addToast({ type: 'success', title: enabled ? 'Chat Enabled' : 'Chat Disabled' });
    } catch {
      addToast({ type: 'error', title: 'Failed to toggle chat' });
    }
  };

  const handleFlag = async () => {
    if (!id || !report) return;
    setFlagLoading(true);
    try {
      const newFlagged = !report.isFlagged;
      await moderatorApi.flagPlayerReport(id, newFlagged);
      setReport({ ...report, isFlagged: newFlagged });
    } catch {}
    setFlagLoading(false);
  };

  const ticketIcon = (type: string) => {
    switch (type) {
      case 'report_player': return <Flag size={14} className="text-rose-400" />;
      case 'bug': return <Bug size={14} className="text-amber-400" />;
      case 'help': return <HelpCircle size={14} className="text-primary-400" />;
      default: return <MessageSquare size={14} className="text-white/30" />;
    }
  };

  const ticketLabel = (type: string) => {
    switch (type) {
      case 'report_player': return 'Report Player';
      case 'bug': return 'Bug Report';
      case 'help': return 'Help Request';
      default: return type;
    }
  };

  if (loading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 bg-white/5 rounded-lg animate-pulse" />
        <div className="h-64 bg-white/5 rounded-xl animate-pulse" />
      </div>
    );
  }

  if (error || !report) {
    return (
      <div className="flex flex-col items-center justify-center py-24">
        <AlertTriangle size={48} className="text-white/10 mb-4" />
        <p className="text-white/30 text-sm">{error || 'Report not found'}</p>
        <AnimatedButton variant="secondary" onClick={() => navigate('/moderator/reports')} className="mt-4">Back to Queue</AnimatedButton>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-5xl">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <button onClick={() => navigate('/moderator/reports')} className="flex items-center gap-1.5 text-xs text-white/30 hover:text-white/60 mb-4 transition-all">
          <ArrowLeft size={14} /> Back to Queue
        </button>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            {ticketIcon(report.ticketType)}
            <h1 className="text-xl font-bold text-white">Ticket #{report.id}</h1>
            <span className={`text-[10px] px-2 py-0.5 rounded-full capitalize ${statusColors[report.status] || 'bg-white/5 text-white/30'}`}>{report.status}</span>
            <span className="text-[10px] text-white/40 bg-white/5 px-2 py-0.5 rounded-full">{ticketLabel(report.ticketType)}</span>
          </div>
          <div className="flex items-center gap-2">
            <span className="text-[10px] text-white/30">Status:</span>
            <Select value={report.status} onChange={async (e) => {
              try {
                await moderatorApi.updatePlayerReportStatus(report.id, e.target.value);
                setReport({ ...report, status: e.target.value });
              } catch {}
            }} className="w-36">
              {statusOptions.map((s) => <option key={s} value={s}>{s}</option>)}
            </Select>
          </div>
        </div>
      </motion.div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <GlassCard className="p-0 overflow-hidden" padding="sm">
            <div className="h-[500px]">
              <ReportChatPanel
                reportId={report.id}
                messages={messages}
                currentUserId={user?.id || ''}
                chatEnabled={report.chatEnabled ?? false}
                canToggleChat
                onToggleChat={handleToggleChat}
                onSendMessage={handleSendMessage}
                onSendAttachment={handleSendAttachment}
              />
            </div>
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Ticket Details</h3>
            <div className="space-y-4">
              <div>
                <span className="text-[10px] text-white/30 uppercase tracking-wider">Type</span>
                <p className="text-sm text-white/80 flex items-center gap-2 mt-1">
                  {ticketIcon(report.ticketType)} {ticketLabel(report.ticketType)}
                </p>
              </div>
              {report.playerName && (
                <div>
                  <span className="text-[10px] text-white/30 uppercase tracking-wider">Player</span>
                  <p className="text-sm text-white/80 mt-1">{report.playerName}</p>
                </div>
              )}
              <div>
                <span className="text-[10px] text-white/30 uppercase tracking-wider">Reason</span>
                <p className="text-sm text-white/80 mt-1">{report.reason}</p>
              </div>
              <div>
                <span className="text-[10px] text-white/30 uppercase tracking-wider">Description</span>
                <p className="text-xs text-white/60 mt-1 whitespace-pre-wrap">{report.description}</p>
              </div>
              <div>
                <span className="text-[10px] text-white/30 uppercase tracking-wider">Created</span>
                <p className="text-sm text-white/80 mt-1">{new Date(report.createdAt).toLocaleString()}</p>
              </div>
              {report.result && (
                <div>
                  <span className="text-[10px] text-white/30 uppercase tracking-wider">Result</span>
                  <p className="text-sm text-white/80 mt-1">{report.result}</p>
                </div>
              )}
              {report.attachmentUrl && (
                <div>
                  <span className="text-[10px] text-white/30 uppercase tracking-wider">Submitted Attachment</span>
                  <a href={report.attachmentUrl} target="_blank" rel="noopener noreferrer"
                    className="block mt-1.5">
                    <img src={report.attachmentUrl} alt="submitted attachment" className="max-w-full max-h-32 rounded-lg object-cover border border-white/10" />
                  </a>
                </div>
              )}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">Actions</h3>
            <div className="space-y-2">
              <AnimatedButton variant={report.isFlagged ? "secondary" : "gradient"} fullWidth
                icon={report.isFlagged ? <FlagOff size={12} /> : <Flag size={12} />}
                onClick={handleFlag}
                disabled={flagLoading}>
                {report.isFlagged ? 'Remove Flag' : 'Flag Report'}
              </AnimatedButton>
              <AnimatedButton variant="primary" fullWidth
                onClick={async () => {
                  try {
                    await moderatorApi.updatePlayerReportStatus(report.id, 'investigating');
                    setReport({ ...report, status: 'investigating' });
                  } catch {}
                }}
                disabled={report.status === 'investigating'}>
                Start Investigation
              </AnimatedButton>
              <AnimatedButton variant="success" fullWidth
                onClick={async () => {
                  try {
                    await moderatorApi.updatePlayerReportStatus(report.id, 'resolved');
                    setReport({ ...report, status: 'resolved' });
                  } catch {}
                }}
                disabled={report.status === 'resolved'}>
                Resolve
              </AnimatedButton>
              <AnimatedButton variant="secondary" fullWidth
                onClick={async () => {
                  try {
                    await moderatorApi.updatePlayerReportStatus(report.id, 'dismissed');
                    setReport({ ...report, status: 'dismissed' });
                  } catch {}
                }}
                disabled={report.status === 'dismissed'}>
                Dismiss
              </AnimatedButton>
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
