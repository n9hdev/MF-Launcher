import { useState, useEffect, useRef } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import { Flag, Send, AlertTriangle, Paperclip, X, Image, MessageSquare, Bug, HelpCircle } from 'lucide-react';
import { reportApi } from '../../services/reports';
import type { IPlayerReport } from '../../services/reports';
import { GlassCard } from '../../components/ui/GlassCard';
import { Select } from '../../components/ui/Select';
import { AnimatedButton } from '../../components/ui/AnimatedButton';

const MAX_FILE_SIZE = 20 * 1024 * 1024;

export function PlayerReportsPage() {
  const navigate = useNavigate();
  const [ticketType, setTicketType] = useState('report_player');
  const [playerName, setPlayerName] = useState('');
  const [reason, setReason] = useState('');
  const [description, setDescription] = useState('');
  const [attachmentFile, setAttachmentFile] = useState<File | null>(null);
  const [attachmentPreview, setAttachmentPreview] = useState<string | null>(null);
  const [reports, setReports] = useState<IPlayerReport[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const allowedTypes = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'application/pdf', 'text/plain', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'video/mp4', 'video/webm'];

  useEffect(() => {
    reportApi.getMyReports().then(({ data }) => setReports(data)).catch((err) => console.error('[PlayerReportsPage] failed to fetch', err));
  }, []);

  const handleFilePick = () => fileRef.current?.click();

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    if (file.size > MAX_FILE_SIZE) {
      setError('File exceeds 20 MB limit');
      return;
    }
    if (!allowedTypes.includes(file.type)) {
      setError('File type not allowed. Accepted: images, PDF, DOC, TXT, MP4, WebM');
      return;
    }
    setAttachmentFile(file);
    setError(null);
    if (file.type.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = () => setAttachmentPreview(reader.result as string);
      reader.readAsDataURL(file);
    } else {
      setAttachmentPreview(null);
    }
  };

  const removeAttachment = () => {
    setAttachmentFile(null);
    setAttachmentPreview(null);
    if (fileRef.current) fileRef.current.value = '';
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (ticketType === 'report_player' && !playerName.trim()) {
      setError('Please enter the player name');
      return;
    }
    if (!reason) {
      setError('Please select a reason');
      return;
    }
    setSubmitting(true);
    try {
      let attachmentUrl = '';
      if (attachmentFile) {
        const { data: uploadData } = await reportApi.uploadAttachment(attachmentFile);
        attachmentUrl = uploadData.url;
      }

      const { data } = await reportApi.submitReport({
        ticketType,
        playerName: ticketType === 'report_player' ? playerName.trim() : '',
        reason,
        description: attachmentUrl ? `${description}\n\n[Attachment](${attachmentUrl})` : description,
      });
      setReports((prev) => [{ ...data, attachmentUrl }, ...prev]);
      setPlayerName('');
      setReason('');
      setDescription('');
      removeAttachment();
    } catch {
      setError('Failed to submit. Please try again.');
    } finally {
      setSubmitting(false);
    }
  };

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
      case 'report_player': return <Flag size={12} className="text-rose-400" />;
      case 'bug': return <Bug size={12} className="text-amber-400" />;
      case 'help': return <HelpCircle size={12} className="text-primary-400" />;
      default: return <MessageSquare size={12} className="text-white/30" />;
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

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Submit a Ticket</h1>
        <p className="text-sm text-white/30 mt-0.5">Report a player, file a bug, or request help</p>
      </motion.div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-4">New Ticket</h3>
            <form onSubmit={handleSubmit} className="space-y-4">
              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Ticket Type</label>
                <div className="flex gap-2">
                  {(['report_player', 'bug', 'help'] as const).map((type) => (
                    <button
                      key={type}
                      type="button"
                      onClick={() => setTicketType(type)}
                      className={`flex-1 text-xs px-3 py-2.5 rounded-xl border transition-all flex items-center justify-center gap-1.5 ${
                        ticketType === type
                          ? 'bg-primary-500/15 border-primary-500/30 text-primary-300'
                          : 'bg-white/5 border-white/10 text-white/40 hover:text-white/60 hover:border-white/20'
                      }`}
                    >
                      {type === 'report_player' ? <Flag size={12} /> : type === 'bug' ? <Bug size={12} /> : <HelpCircle size={12} />}
                      {ticketLabel(type)}
                    </button>
                  ))}
                </div>
              </div>

              <AnimatePresence>
                {ticketType === 'report_player' && (
                  <motion.div
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: 'auto', opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    transition={{ duration: 0.15 }}
                  >
                    <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Player Name *</label>
                    <input value={playerName} onChange={(e) => setPlayerName(e.target.value)}
                      className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40" placeholder="Enter player username" />
                  </motion.div>
                )}
              </AnimatePresence>

              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Reason *</label>
                <Select value={reason} onChange={(e) => setReason(e.target.value)} placeholder="Select a reason...">
                  {ticketType === 'report_player' && (
                    <>
                      <option value="">Select a reason...</option>
                      <option value="Speed hack">Speed hack</option>
                      <option value="Aimbot / Wallhack">Aimbot / Wallhack</option>
                      <option value="Memory injection">Memory injection</option>
                      <option value="Script abuse">Script abuse</option>
                      <option value="Harassment">Harassment</option>
                      <option value="Other">Other</option>
                    </>
                  )}
                  {ticketType === 'bug' && (
                    <>
                      <option value="">Select a category...</option>
                      <option value="Crash / Freeze">Crash / Freeze</option>
                      <option value="UI / Visual">UI / Visual</option>
                      <option value="Gameplay">Gameplay</option>
                      <option value="Performance">Performance</option>
                      <option value="Other">Other</option>
                    </>
                  )}
                  {ticketType === 'help' && (
                    <>
                      <option value="">Select a topic...</option>
                      <option value="Account issue">Account issue</option>
                      <option value="Installation">Installation</option>
                      <option value="How to play">How to play</option>
                      <option value="Report status">Report status</option>
                      <option value="Other">Other</option>
                    </>
                  )}
                </Select>
              </div>

              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Description</label>
                <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4}
                  className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 text-xs text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 resize-none" placeholder="Describe the issue in detail..." />
              </div>

              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5">Attachment (optional, max 20 MB)</label>
                <input ref={fileRef} type="file" accept="image/*,.pdf,.doc,.docx,.txt,.mp4,.webm" onChange={handleFileChange} className="hidden" />
                {!attachmentFile ? (
                  <button type="button" onClick={handleFilePick}
                    className="w-full flex items-center justify-center gap-2 bg-white/5 border border-dashed border-white/10 rounded-xl px-4 py-3 text-xs text-white/30 hover:text-white/50 hover:border-white/20 transition-all">
                    <Paperclip size={14} /> Click to attach a file
                  </button>
                ) : (
                  <div className="flex items-center gap-3 bg-white/5 border border-white/10 rounded-xl px-4 py-2.5">
                    {attachmentPreview ? (
                      <img src={attachmentPreview} alt="preview" className="w-10 h-10 rounded-lg object-cover" />
                    ) : (
                      <Image size={20} className="text-white/30" />
                    )}
                    <div className="flex-1 min-w-0">
                      <p className="text-xs text-white/60 truncate">{attachmentFile.name}</p>
                      <p className="text-[10px] text-white/30">{(attachmentFile.size / 1024 / 1024).toFixed(1)} MB</p>
                    </div>
                    <button type="button" onClick={removeAttachment} className="text-white/30 hover:text-rose-400"><X size={14} /></button>
                  </div>
                )}
              </div>

              {error && (
                <div className="flex items-center gap-2 text-rose-400 text-xs bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-2">
                  <AlertTriangle size={14} /> {error}
                </div>
              )}

              <AnimatedButton type="submit" variant="gradient" icon={<Send size={14} />}
                disabled={ticketType === 'report_player' && !playerName || !reason}
                loading={submitting}>
                Submit Ticket
              </AnimatedButton>
            </form>
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Guidelines</h3>
            <div className="space-y-2 text-xs text-white/40">
              <p>• Reports are reviewed by moderators</p>
              <p>• Attach evidence when possible (images, videos, docs)</p>
              <p>• File size limit: 20 MB</p>
              <p>• False reports may affect your trust score</p>
              <p>• Use chat to communicate with moderators</p>
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">My Tickets</h3>
            <div className="space-y-2">
              {reports.slice(0, 10).map((r) => (
                <button
                  key={r.id}
                  onClick={() => navigate(`/player/reports/${r.id}`)}
                  className="w-full flex items-center justify-between py-2 border-b border-white/5 last:border-0 cursor-pointer hover:bg-white/[0.02] rounded-lg px-2 -mx-2 transition-colors text-left"
                >
                  <div className="flex items-center gap-2 flex-1 min-w-0">
                    {ticketIcon(r.ticketType)}
                    <div className="min-w-0">
                      <p className="text-xs text-white/60 truncate">{r.playerName || ticketLabel(r.ticketType)}</p>
                      <p className="text-[10px] text-white/30 truncate">{r.reason}</p>
                    </div>
                  </div>
                  <div className="flex items-center gap-2 shrink-0">
                    <span className={`text-[10px] px-1.5 py-0.5 rounded capitalize ${statusColor(r.status)}`}>
                      {r.status}
                    </span>
                    <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" className="text-white/20">
                      <polyline points="9 18 15 12 9 6" />
                    </svg>
                  </div>
                </button>
              ))}
              {reports.length === 0 && (
                <p className="text-xs text-white/20 text-center py-4">No tickets submitted yet</p>
              )}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
