import { useEffect, useState, useRef } from 'react';
import { motion } from 'framer-motion';
import { ScrollText, CheckCircle, XCircle, Clock, Send, MessageSquare, User } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { AnimatedModal } from '../../components/ui/AnimatedModal';
import { DataTable } from '../../components/ui/DataTable';
import { MetricCard } from '../../components/ui/MetricCard';
import { adminApi } from '../../services/admin';
import { useUIStore } from '../../stores/uiStore';
import type { IAdminAppeal, IAppealMessage } from '../../services/admin';

export function AppealsPage() {
  const [appeals, setAppeals] = useState<IAdminAppeal[]>([]);
  const [detailModalOpen, setDetailModalOpen] = useState(false);
  const [selectedAppeal, setSelectedAppeal] = useState<IAdminAppeal | null>(null);
  const [messages, setMessages] = useState<IAppealMessage[]>([]);
  const [replyText, setReplyText] = useState('');
  const [sendingReply, setSendingReply] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const { addToast } = useUIStore();

  useEffect(() => {
    adminApi.getAppeals().then(({ data }) => setAppeals(data)).catch((err) => console.error('[AppealsPage] failed to fetch', err));
  }, []);

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const openDetail = async (appeal: IAdminAppeal) => {
    setSelectedAppeal(appeal);
    setDetailModalOpen(true);
    if (appeal.id) {
      try {
        const { data } = await adminApi.getAppealMessages(appeal.id);
        setMessages(data.messages || data || []);
      } catch {
        setMessages([]);
      }
    }
  };

  const handleApprove = async () => {
    if (!selectedAppeal) return;
    try {
      await adminApi.updateAppealStatus(selectedAppeal.id, { status: 'Approved', reviewer: 'Admin' });
      setAppeals((prev) => prev.map((a) => a.id === selectedAppeal.id ? { ...a, status: 'Approved', reviewer: 'Admin' } : a));
      setDetailModalOpen(false);
      addToast({ type: 'success', title: 'Appeal Approved' });
    } catch {
      addToast({ type: 'error', title: 'Failed to approve appeal' });
    }
  };

  const handleDeny = async () => {
    if (!selectedAppeal) return;
    try {
      await adminApi.updateAppealStatus(selectedAppeal.id, { status: 'Denied', reviewer: 'Admin' });
      setAppeals((prev) => prev.map((a) => a.id === selectedAppeal.id ? { ...a, status: 'Denied', reviewer: 'Admin' } : a));
      setDetailModalOpen(false);
      addToast({ type: 'info', title: 'Appeal Denied' });
    } catch {
      addToast({ type: 'error', title: 'Failed to deny appeal' });
    }
  };

  const handleSendReply = async () => {
    if (!selectedAppeal || !replyText.trim()) return;
    setSendingReply(true);
    try {
      const { data } = await adminApi.sendAppealMessage(selectedAppeal.id, replyText);
      setMessages((prev) => [...prev, data]);
      setReplyText('');
    } catch {
      addToast({ type: 'error', title: 'Failed to send reply' });
    }
    setSendingReply(false);
  };

  const pending = appeals.filter((a) => a.status === 'Pending').length;
  const approved = appeals.filter((a) => a.status === 'Approved').length;
  const denied = appeals.filter((a) => a.status === 'Denied').length;

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Appeals</h1>
          <p className="text-sm text-white/30 mt-0.5">Review player ban appeals</p>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Total Appeals" value={String(appeals.length)} icon={<ScrollText size={16} />} />
        <MetricCard title="Pending" value={String(pending)} trend="up" icon={<Clock size={16} />} />
        <MetricCard title="Approved" value={String(approved)} trend="up" icon={<CheckCircle size={16} />} />
        <MetricCard title="Denied" value={String(denied)} trend="down" icon={<XCircle size={16} />} />
      </div>

      <GlassCard className="p-6">
        <DataTable
          columns={[
            { key: 'player', label: 'Player', sortable: true },
            { key: 'reason', label: 'Reason', sortable: true },
            { key: 'banType', label: 'Ban Type', sortable: true },
            {
              key: 'status', label: 'Status', sortable: true,
              render: (item: IAdminAppeal) => (
                <span className={`text-[10px] px-2 py-0.5 rounded-full ${
                  item.status === 'Approved' ? 'bg-emerald-500/20 text-emerald-400' :
                  item.status === 'Denied' ? 'bg-rose-500/20 text-rose-400' :
                  'bg-amber-500/20 text-amber-400'
                }`}>{item.status}</span>
              ),
            },
            { key: 'date', label: 'Date', sortable: true },
            { key: 'reviewer', label: 'Reviewer' },
          ]}
          data={appeals}
          keyExtractor={(item) => item.id}
          searchable
          searchKeys={['player', 'reason']}
          onRowClick={(item) => openDetail(item)}
        />
      </GlassCard>

      <AnimatedModal open={detailModalOpen} onClose={() => setDetailModalOpen(false)} title="Appeal Details" width="max-w-3xl">
        {selectedAppeal && (
          <div className="space-y-4 max-h-[70vh] flex flex-col">
            <div className="grid grid-cols-2 gap-3 pb-3 border-b border-white/5">
              <div><span className="text-[10px] text-white/40 block">Player</span><span className="text-xs text-white/70">{selectedAppeal.player}</span></div>
              <div><span className="text-[10px] text-white/40 block">Status</span><span className={`text-[10px] px-2 py-0.5 rounded-full ${
                selectedAppeal.status === 'Approved' ? 'bg-emerald-500/20 text-emerald-400' :
                selectedAppeal.status === 'Denied' ? 'bg-rose-500/20 text-rose-400' :
                'bg-amber-500/20 text-amber-400'
              }`}>{selectedAppeal.status}</span></div>
              <div><span className="text-[10px] text-white/40 block">Ban Type</span><span className="text-xs text-white/70">{selectedAppeal.banType}</span></div>
              <div><span className="text-[10px] text-white/40 block">Date</span><span className="text-xs text-white/70">{selectedAppeal.date}</span></div>
              <div className="col-span-2"><span className="text-[10px] text-white/40 block">Reason</span><span className="text-xs text-white/70">{selectedAppeal.reason}</span></div>
            </div>

            <div className="flex-1 overflow-y-auto space-y-3 min-h-0 max-h-60">
              {messages.length === 0 && (
                <div className="flex flex-col items-center justify-center py-8 text-white/20">
                  <MessageSquare size={32} />
                  <p className="text-xs mt-2">No messages yet</p>
                </div>
              )}
              {messages.map((msg) => {
                const isAdmin = msg.senderName !== selectedAppeal.player;
                return (
                  <motion.div
                    key={msg.id}
                    initial={{ opacity: 0, y: 4 }}
                    animate={{ opacity: 1, y: 0 }}
                    className={`flex ${isAdmin ? 'justify-end' : 'justify-start'}`}
                  >
                    <div className={`max-w-[80%] px-3 py-2 rounded-2xl ${
                      isAdmin
                        ? 'bg-primary-500/20 border border-primary-500/20'
                        : 'bg-white/5 border border-white/10'
                    }`}>
                      <div className="flex items-center gap-2 mb-0.5">
                        <User size={10} className={isAdmin ? 'text-primary-300' : 'text-white/30'} />
                        <span className={`text-[10px] font-medium ${isAdmin ? 'text-primary-300' : 'text-white/40'}`}>
                          {isAdmin ? msg.senderName : selectedAppeal.player}
                        </span>
                        <span className="text-[9px] text-white/20">{new Date(msg.createdAt).toLocaleString()}</span>
                      </div>
                      <p className="text-xs text-white/70">{msg.message}</p>
                    </div>
                  </motion.div>
                );
              })}
              <div ref={messagesEndRef} />
            </div>

            <div className="pt-3 border-t border-white/5 space-y-3">
              <div className="flex gap-2">
                <textarea
                  value={replyText}
                  onChange={(e) => setReplyText(e.target.value)}
                  placeholder="Type a reply..."
                  rows={2}
                  className="flex-1 bg-white/5 border border-white/10 rounded-xl px-3 py-2 text-xs text-white/70 placeholder-white/20 outline-none focus:border-primary-500/40 resize-none"
                  onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSendReply(); } }}
                />
                <AnimatedButton
                  variant="primary"
                  onClick={handleSendReply}
                  loading={sendingReply}
                  disabled={!replyText.trim()}
                  icon={<Send size={12} />}
                  className="self-end"
                />
              </div>
              {selectedAppeal.status === 'Pending' && (
                <div className="flex gap-2">
                  <AnimatedButton variant="secondary" onClick={handleDeny} fullWidth icon={<XCircle size={12} />}>Deny</AnimatedButton>
                  <AnimatedButton variant="gradient" onClick={handleApprove} fullWidth icon={<CheckCircle size={12} />}>Approve</AnimatedButton>
                </div>
              )}
            </div>
          </div>
        )}
      </AnimatedModal>
    </div>
  );
}
