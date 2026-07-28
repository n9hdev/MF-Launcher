import { useState, useEffect, useRef } from 'react';
import { Send, Paperclip, Image, FileText, Smile, X, AlertCircle } from 'lucide-react';
import { motion, AnimatePresence } from 'framer-motion';
import { EmojiPicker } from './EmojiPicker';
import { AnimatedModal } from '../ui/AnimatedModal';
import type { IReportMessage } from '../../services/reports';

interface IReportChatPanelProps {
  reportId: string;
  messages: IReportMessage[];
  currentUserId: string;
  chatEnabled: boolean;
  onToggleChat?: (enabled: boolean) => void;
  canToggleChat?: boolean;
  onSendMessage: (message: string) => Promise<void>;
  onSendAttachment: (file: File) => Promise<void>;
  onLoadMessages?: () => void;
}

interface IAttachmentPreview {
  file: File;
  preview: string | null;
}

const ALLOWED_TYPES = ['image/jpeg', 'image/png', 'image/gif', 'image/webp', 'application/pdf', 'text/plain', 'application/msword', 'application/vnd.openxmlformats-officedocument.wordprocessingml.document', 'video/mp4', 'video/webm'];
const MAX_FILE_SIZE = 20 * 1024 * 1024;

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

export function ReportChatPanel({
  reportId, messages, currentUserId, chatEnabled, onToggleChat,
  canToggleChat, onSendMessage, onSendAttachment, onLoadMessages,
}: IReportChatPanelProps) {
  const [text, setText] = useState('');
  const [sending, setSending] = useState(false);
  const [emojiOpen, setEmojiOpen] = useState(false);
  const [pendingAttachments, setPendingAttachments] = useState<IAttachmentPreview[]>([]);
  const [sendingFiles, setSendingFiles] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const bottomRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const autoResize = () => {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = Math.min(el.scrollHeight, 120) + 'px';
  };

  useEffect(() => { autoResize(); }, [text]);

  const handleSend = async () => {
    const msg = text.trim();
    if (!msg) return;
    setSending(true);
    setText('');
    try {
      await onSendMessage(msg);
      setSendError(null);
    } catch {
      setText(msg);
      setSendError('Failed to send message');
    } finally {
      setSending(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  const handleFilePick = () => fileInputRef.current?.click();

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files || []);
    if (files.length === 0) return;

    const valid: IAttachmentPreview[] = [];
    for (const file of files) {
      if (file.size > MAX_FILE_SIZE) {
        setSendError(`"${file.name}" exceeds the 20 MB limit`);
        continue;
      }
      if (!ALLOWED_TYPES.includes(file.type)) {
        setSendError(`"${file.name}" has an unsupported file type`);
        continue;
      }
      let preview: string | null = null;
      if (file.type.startsWith('image/')) {
        const reader = new FileReader();
        reader.onload = () => {
          setPendingAttachments((prev) =>
            prev.map((a) => a.file === file ? { ...a, preview: reader.result as string } : a)
          );
        };
        reader.readAsDataURL(file);
      }
      valid.push({ file, preview });
    }
    if (valid.length > 0) {
      setPendingAttachments((prev) => [...prev, ...valid]);
      setSendError(null);
    }
    if (e.target) e.target.value = '';
  };

  const removePending = (idx: number) => {
    setPendingAttachments((prev) => prev.filter((_, i) => i !== idx));
  };

  const confirmSendAttachments = async () => {
    if (pendingAttachments.length === 0) return;
    setSendingFiles(true);
    setSendError(null);
    let hasError = false;
    for (const { file } of pendingAttachments) {
      try {
        await onSendAttachment(file);
      } catch {
        setSendError(`Failed to send "${file.name}"`);
        hasError = true;
      }
    }
    if (!hasError) setPendingAttachments([]);
    setSendingFiles(false);
  };

  const canSend = chatEnabled && !sending && !sendingFiles;

  const renderAttachment = (url: string) => {
    const ext = url.split('.').pop()?.toLowerCase();
    const isImage = /^(png|jpe?g|gif|webp|bmp|svg)$/.test(ext || '');
    if (isImage) {
      return (
        <a href={url} target="_blank" rel="noopener noreferrer" className="block mt-1.5">
          <img src={url} alt="attachment" className="max-w-[200px] max-h-[150px] rounded-lg object-cover border border-white/10" />
        </a>
      );
    }
    return (
      <a href={url} target="_blank" rel="noopener noreferrer"
        className="inline-flex items-center gap-1.5 mt-1.5 text-[10px] text-primary-300 bg-primary-500/10 rounded-lg px-2.5 py-1.5 border border-primary-500/20 hover:bg-primary-500/20"
      >
        <FileText size={12} />
        View Attachment
      </a>
    );
  };

  return (
    <div className="flex flex-col h-full min-h-[300px]">
      {canToggleChat && onToggleChat && (
        <div className="flex items-center justify-between px-4 py-2 border-b border-white/5">
          <span className="text-[10px] text-white/40 uppercase tracking-wider">Chat</span>
          <label className="flex items-center gap-2 cursor-pointer">
            <span className="text-[10px] text-white/50">{chatEnabled ? 'Enabled' : 'Disabled'}</span>
            <button
              type="button"
              onClick={() => onToggleChat(!chatEnabled)}
              className={`relative w-9 h-5 rounded-full transition-colors ${chatEnabled ? 'bg-emerald-500/50' : 'bg-white/10'}`}
            >
              <motion.div
                animate={{ x: chatEnabled ? 16 : 2 }}
                transition={{ type: 'spring', stiffness: 500, damping: 30 }}
                className="absolute top-0.5 w-4 h-4 rounded-full bg-white shadow"
              />
            </button>
          </label>
        </div>
      )}

      <div className="flex-1 overflow-y-auto p-4 space-y-3">
        {messages.length === 0 && (
          <div className="flex items-center justify-center h-full">
            <p className="text-xs text-white/20">No messages yet</p>
          </div>
        )}
        {messages.map((msg) => {
          const isMine = msg.senderId === currentUserId;
          return (
            <div key={msg.id} className={`flex ${isMine ? 'justify-end' : 'justify-start'}`}>
              <div
                className={`max-w-[75%] rounded-xl px-3 py-2 ${
                  isMine
                    ? 'bg-primary-500/20 border border-primary-500/20 text-white/90'
                    : 'bg-white/5 border border-white/5 text-white/70'
                }`}
              >
                {!isMine && (
                  <p className="text-[9px] text-white/30 mb-0.5">{msg.senderName}</p>
                )}
                {msg.message && <p className="text-xs whitespace-pre-wrap break-words">{msg.message}</p>}
                {msg.attachmentUrl && renderAttachment(msg.attachmentUrl)}
                <p className={`text-[9px] mt-1 ${isMine ? 'text-white/30 text-right' : 'text-white/20'}`}>
                  {new Date(msg.createdAt).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}
                </p>
              </div>
            </div>
          );
        })}
        <div ref={bottomRef} />
      </div>

      {/* Attachment confirmation modal */}
      <AnimatedModal
        open={pendingAttachments.length > 0 && !sendingFiles}
        onClose={() => { if (!sendingFiles) setPendingAttachments([]); }}
        title={`Send ${pendingAttachments.length} file${pendingAttachments.length !== 1 ? 's' : ''}`}
      >
        <div className="space-y-3 max-h-60 overflow-y-auto">
          {pendingAttachments.map((a, i) => (
            <div key={i} className="flex items-center gap-3 bg-white/5 rounded-xl px-3 py-2 border border-white/5">
              {a.preview ? (
                <img src={a.preview} alt="" className="w-10 h-10 rounded-lg object-cover flex-shrink-0" />
              ) : (
                <div className="w-10 h-10 rounded-lg bg-white/5 flex items-center justify-center flex-shrink-0">
                  <FileText size={16} className="text-white/30" />
                </div>
              )}
              <div className="flex-1 min-w-0">
                <p className="text-xs text-white/70 truncate">{a.file.name}</p>
                <p className="text-[10px] text-white/30">{formatSize(a.file.size)}</p>
              </div>
              <button
                type="button"
                onClick={() => removePending(i)}
                className="w-6 h-6 flex items-center justify-center rounded-md text-white/30 hover:text-rose-400 hover:bg-white/5"
              >
                <X size={12} />
              </button>
            </div>
          ))}
        </div>
        <div className="flex justify-end gap-2 mt-4">
          <button
            type="button"
            onClick={() => setPendingAttachments([])}
            className="px-4 py-2 rounded-xl text-xs text-white/50 hover:text-white/70 hover:bg-white/5 border border-white/10"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={confirmSendAttachments}
            className="px-4 py-2 rounded-xl text-xs text-white bg-primary-600 hover:bg-primary-500 border border-primary-500/30"
          >
            Send {pendingAttachments.length > 1 ? `All (${pendingAttachments.length})` : ''}
          </button>
        </div>
      </AnimatedModal>

      {/* Sending overlay */}
      <AnimatedModal open={sendingFiles} onClose={() => {}} title="Sending files...">
        <div className="flex items-center justify-center py-8">
          <motion.div
            animate={{ rotate: 360 }}
            transition={{ repeat: Infinity, duration: 1, ease: 'linear' }}
          >
            <Send size={24} className="text-primary-400" />
          </motion.div>
        </div>
      </AnimatedModal>

      {/* Composer */}
      <div className="border-t border-white/5">
        {!chatEnabled && (
          <div className="flex items-center justify-center py-3 bg-black/40">
            <p className="text-xs text-white/40">Chat is disabled for this report</p>
          </div>
        )}
        {sendError && (
          <div className="flex items-center gap-2 px-4 py-2 bg-rose-500/10 border-b border-rose-500/20">
            <AlertCircle size={12} className="text-rose-400 flex-shrink-0" />
            <p className="text-[10px] text-rose-300">{sendError}</p>
            <button onClick={() => setSendError(null)} className="ml-auto text-rose-400/50 hover:text-rose-400"><X size={10} /></button>
          </div>
        )}

        <div className="flex items-center gap-2 px-3 py-3">
          {/* Attachment button */}
          <button
            type="button"
            onClick={handleFilePick}
            disabled={!canSend}
            className="flex items-center justify-center w-10 h-10 rounded-xl text-white/30 hover:text-white/60 hover:bg-white/5 disabled:opacity-30 disabled:cursor-not-allowed transition-colors flex-shrink-0"
          >
            <Paperclip size={16} />
          </button>
          <input
            ref={fileInputRef}
            type="file"
            multiple
            accept="image/*,.pdf,.doc,.docx,.txt,.mp4,.webm"
            onChange={handleFileChange}
            className="hidden"
          />

          {/* Input container */}
          <div className="relative flex-1">
            <textarea
              ref={textareaRef}
              value={text}
              onChange={(e) => setText(e.target.value)}
              onKeyDown={handleKeyDown}
              disabled={!canSend}
              rows={1}
              placeholder="Type a message..."
              className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 pr-10 text-xs leading-5 text-white/70 placeholder-white/20 outline-none focus:border-primary-500/30 resize-none disabled:opacity-30 transition-colors"
              style={{ minHeight: 40, maxHeight: 120 }}
            />
            {/* Emoji button — inside input, absolutely positioned */}
            <button
              type="button"
              onClick={() => setEmojiOpen(!emojiOpen)}
              disabled={!canSend}
              className="absolute right-2.5 top-1/2 -translate-y-1/2 flex items-center justify-center w-5 h-5 text-white/30 hover:text-white/60 disabled:opacity-30 transition-colors"
            >
              <Smile size={16} />
            </button>
            <EmojiPicker
              open={emojiOpen}
              onClose={() => setEmojiOpen(false)}
              onSelect={(emoji) => setText((t) => t + emoji)}
            />
          </div>

          {/* Send button */}
          <button
            type="button"
            onClick={handleSend}
            disabled={!canSend || (!text.trim() && pendingAttachments.length === 0)}
            className="flex items-center justify-center w-10 h-10 rounded-xl bg-primary-500/20 text-primary-300 hover:bg-primary-500/30 disabled:opacity-30 disabled:cursor-not-allowed transition-colors flex-shrink-0"
          >
            <Send size={16} />
          </button>
        </div>
      </div>
    </div>
  );
}
