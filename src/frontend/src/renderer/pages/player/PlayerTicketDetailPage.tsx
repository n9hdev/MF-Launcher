import { useState, useEffect, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { motion } from 'framer-motion';
import { ArrowLeft, Flag, Bug, HelpCircle, MessageSquare, AlertTriangle, Clock, Send } from 'lucide-react';
import { reportApi } from '../../services/reports';
import type { IPlayerReport, IReportMessage } from '../../services/reports';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { ReportChatPanel } from '../../components/chat/ReportChatPanel';
import { useAuthStore } from '../../stores/authStore';
import { useUIStore } from '../../stores/uiStore';

const statusColor = (status: string) => {
  switch (status) {
    case 'resolved': return 'bg-emerald-500/20 text-emerald-400';
    case 'investigating': return 'bg-amber-500/20 text-amber-400';
    case 'pending': return 'bg-white/5 text-white/30';
    case 'dismissed': return 'bg-rose-500/20 text-rose-400';
    default: return 'bg-white/5 text-white/30';
  }
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

export function PlayerTicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);
  const { addToast } = useUIStore();
  const [report, setReport] = useState<IPlayerReport | null>(null);
  const [messages, setMessages] = useState<IReportMessage[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchReport = useCallback(async () => {
    if (!id) return;
    try {
      const { data } = await reportApi.getReport(id);
      setReport(data);
      setError(null);
    } catch {
      setError('Failed to load ticket');
    } finally {
      setLoading(false);
    }
  }, [id]);

  const fetchMessages = useCallback(async () => {
    if (!id) return;
    try {
      const { data } = await reportApi.getMessages(id);
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
      reportApi.getReport(id).then(({ data }) => setReport(data)).catch(() => {});
    }, 5000);
    return () => clearInterval(interval);
  }, [id]);

  const handleSendMessage = async (message: string) => {
    if (!id) return;
    try {
      const { data } = await reportApi.sendMessage(id, message);
      setMessages((prev) => [...prev, data.message]);
    } catch {
      addToast({ type: 'error', title: 'Failed to send message' });
    }
  };

  const handleSendAttachment = async (file: File) => {
    if (!id) return;
    try {
      const { data } = await reportApi.sendAttachment(id, file);
      setMessages((prev) => [...prev, data.message]);
    } catch {
      addToast({ type: 'error', title: 'Failed to send attachment' });
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
        <p className="text-white/30 text-sm">{error || 'Ticket not found'}</p>
        <AnimatedButton variant="secondary" onClick={() => navigate('/player/reports')} className="mt-4">
          Back to Tickets
        </AnimatedButton>
      </div>
    );
  }

  return (
    <div className="space-y-6 max-w-4xl">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <button
          onClick={() => navigate('/player/reports')}
          className="flex items-center gap-1.5 text-xs text-white/30 hover:text-white/60 mb-4 transition-all"
        >
          <ArrowLeft size={14} /> Back to Tickets
        </button>

        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            {ticketIcon(report.ticketType)}
            <div>
              <h1 className="text-xl font-bold text-white">
                {report.playerName || ticketLabel(report.ticketType)}
              </h1>
              <p className="text-sm text-white/30 mt-0.5">{report.reason}</p>
            </div>
          </div>
          <div className="flex items-center gap-3">
            <span className={`text-[10px] px-2 py-0.5 rounded-full font-medium ${statusColor(report.status)}`}>
              {report.status}
            </span>
            <span className="text-xs text-white/30">
              <Clock size={12} className="inline mr-1" />
              {new Date(report.createdAt).toLocaleDateString()}
            </span>
          </div>
        </div>
      </motion.div>

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

      <GlassCard className="p-0 overflow-hidden h-[500px]">
        <ReportChatPanel
          reportId={report.id}
          messages={messages}
          currentUserId={user?.id || ''}
          chatEnabled={report.chatEnabled ?? false}
          onSendMessage={handleSendMessage}
          onSendAttachment={handleSendAttachment}
        />
      </GlassCard>
    </div>
  );
}