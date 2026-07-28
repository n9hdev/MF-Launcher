import { useState, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { Shield, Send, MessageSquare, AlertTriangle, CheckCircle, Clock } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { useAuthStore } from '../../stores/authStore';
import { authApi } from '../../services/auth';

interface IAppealMessage {
  id: string;
  appealId: string;
  senderId: string;
  senderName: string;
  message: string;
  createdAt: string;
}

interface IAppealData {
  id: string;
  player: string;
  playerId: string;
  banId: string;
  reason: string;
  banType: string;
  status: string;
  date: string;
  reviewer: string;
  messages: IAppealMessage[];
}

export function AppealTicketPage() {
  const { user, banInfo } = useAuthStore();
  const [appeal, setAppeal] = useState<IAppealData | null>(null);
  const [hasAppeal, setHasAppeal] = useState(false);
  const [loading, setLoading] = useState(true);
  const [newMessage, setNewMessage] = useState('');
  const [sending, setSending] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    authApi.getMyAppeal().then(({ data }) => {
      const d = data as { hasAppeal: boolean; appeal: IAppealData | null };
      setHasAppeal(d.hasAppeal);
      if (d.hasAppeal && d.appeal) setAppeal(d.appeal);
    }).catch(() => {}).finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [appeal?.messages]);

  const handleSend = async () => {
    if (!newMessage.trim()) return;
    setSending(true);
    try {
      const { data } = await authApi.sendAppealMessage(newMessage);
      const d = data as { success: boolean; message: IAppealMessage };
      if (appeal) {
        setAppeal({
          ...appeal,
          messages: [...appeal.messages, d.message],
        });
      }
      setNewMessage('');
    } catch { /* ignore */ }
    setSending(false);
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center h-full">
        <div className="animate-spin h-8 w-8 border-2 border-primary-500 border-t-transparent rounded-full" />
      </div>
    );
  }

  const statusColor = appeal?.status === 'Approved' ? 'emerald' : appeal?.status === 'Denied' ? 'rose' : 'amber';
  const StatusIcon = appeal?.status === 'Approved' ? CheckCircle : appeal?.status === 'Denied' ? AlertTriangle : Clock;

  return (
    <div className="h-full flex flex-col gap-4 max-w-3xl mx-auto">
      <motion.div initial={{ opacity: 0, y: -8 }} animate={{ opacity: 1, y: 0 }}>
        <GlassCard className="p-4 flex items-center gap-4">
          <div className={`w-10 h-10 rounded-xl bg-${statusColor}-500/20 flex items-center justify-center`}>
            <Shield size={20} className={`text-${statusColor}-400`} />
          </div>
          <div className="flex-1">
            <h2 className="text-sm font-bold text-white">Appeal Ticket</h2>
            <p className="text-xs text-white/40">
              {appeal ? `Status: ${appeal.status} · ${appeal.messages.length} message${appeal.messages.length !== 1 ? 's' : ''}` : 'No appeal submitted yet'}
            </p>
          </div>
          {appeal && (
            <div className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-${statusColor}-500/10 border border-${statusColor}-500/20`}>
              <StatusIcon size={12} className={`text-${statusColor}-400`} />
              <span className={`text-xs font-medium text-${statusColor}-400`}>{appeal.status}</span>
            </div>
          )}
        </GlassCard>
      </motion.div>

      {!hasAppeal ? (
        <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="flex-1 flex items-center justify-center">
          <GlassCard className="p-8 text-center max-w-md">
            <MessageSquare size={40} className="text-white/20 mx-auto mb-4" />
            <h3 className="text-lg font-bold text-white mb-2">No Appeal Submitted</h3>
            <p className="text-sm text-white/40">You haven't submitted an appeal yet. Go back to the banned page to submit one.</p>
          </GlassCard>
        </motion.div>
      ) : (
        <>
          <motion.div initial={{ opacity: 0 }} animate={{ opacity: 1 }} className="flex-1 overflow-y-auto space-y-3 px-1">
            {appeal!.messages.map((msg) => {
              const isPlayer = msg.senderId === user?.id;
              return (
                <motion.div
                  key={msg.id}
                  initial={{ opacity: 0, y: 8 }}
                  animate={{ opacity: 1, y: 0 }}
                  className={`flex ${isPlayer ? 'justify-end' : 'justify-start'}`}
                >
                  <div
                    className={`max-w-[75%] px-4 py-2.5 rounded-2xl ${
                      isPlayer
                        ? 'bg-primary-500/20 border border-primary-500/20 text-white'
                        : 'bg-white/5 border border-white/10 text-white/80'
                    }`}
                  >
                    <div className="flex items-center gap-2 mb-1">
                      <span className={`text-[10px] font-semibold ${isPlayer ? 'text-primary-300' : 'text-white/40'}`}>
                        {isPlayer ? 'You' : msg.senderName}
                      </span>
                      <span className="text-[9px] text-white/20">{new Date(msg.createdAt).toLocaleString()}</span>
                    </div>
                    <p className="text-sm whitespace-pre-wrap">{msg.message}</p>
                  </div>
                </motion.div>
              );
            })}
            <div ref={messagesEndRef} />
          </motion.div>

          <GlassCard className="p-3">
            <div className="flex gap-2">
              <textarea
                value={newMessage}
                onChange={(e) => setNewMessage(e.target.value)}
                placeholder="Type your message..."
                rows={2}
                className="flex-1 bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 resize-none"
                onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend(); } }}
              />
              <AnimatedButton
                variant="primary"
                onClick={handleSend}
                loading={sending}
                disabled={!newMessage.trim()}
                icon={<Send size={14} />}
                className="self-end"
              />
            </div>
          </GlassCard>
        </>
      )}
    </div>
  );
}
