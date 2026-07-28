import { useEffect, useState, useRef } from 'react';
import { motion } from 'framer-motion';
import { MessageSquare, Send, Users, Paperclip, Smile } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { EmojiPicker } from '../../components/chat/EmojiPicker';
import { modchatApi } from '../../services/modchat';
import { useAuthStore } from '../../stores/authStore';
import type { IChatMessage, IOnlineModerator } from '../../services/modchat';

export function ModChatPage() {
  const currentUser = useAuthStore((s) => s.user);
  const [messages, setMessages] = useState<IChatMessage[]>([]);
  const [onlineMods, setOnlineMods] = useState<IOnlineModerator[]>([]);
  const [input, setInput] = useState('');
  const [showEmoji, setShowEmoji] = useState(false);
  const [sendingAttachment, setSendingAttachment] = useState(false);
  const chatEndRef = useRef<HTMLDivElement>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    const fetch = () => {
      modchatApi.getMessages().then(({ data }) => setMessages(data)).catch(() => {});
      modchatApi.getOnline().then(({ data }) => setOnlineMods(data)).catch(() => {});
    };
    fetch();
    const interval = setInterval(fetch, 10000);
    return () => clearInterval(interval);
  }, []);

  useEffect(() => {
    chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages]);

  const handleSend = async () => {
    if (!input.trim()) return;
    try {
      const { data } = await modchatApi.sendMessage(input.trim());
      setMessages((prev) => [...prev, data]);
      setInput('');
    } catch { /* toast handled by interceptor */ }
  };

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    setSendingAttachment(true);
    try {
      const { data } = await modchatApi.sendAttachment(file);
      setMessages((prev) => [...prev, data]);
    } catch { /* toast handled by interceptor */ }
    setSendingAttachment(false);
    if (fileInputRef.current) fileInputRef.current.value = '';
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Moderation Chat</h1>
          <p className="text-sm text-white/30 mt-0.5">Team communication channel</p>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-6">
        <div className="col-span-3">
          <GlassCard className="p-6 flex flex-col h-[600px]">
            <div className="flex items-center gap-2 pb-4 border-b border-white/5 mb-4">
              <MessageSquare size={14} className="text-primary-400" />
              <span className="text-xs font-semibold text-white/50 uppercase tracking-wider">#moderation</span>
            </div>

            <div className="flex-1 overflow-y-auto space-y-2 mb-4 pr-2">
              {messages.map((msg) => {
                const isMine = msg.userId === currentUser?.id;
                return (
                  <div key={msg.id} className={`flex ${isMine ? 'justify-end' : 'justify-start'}`}>
                    <div className={`max-w-[70%] ${isMine ? 'order-1' : 'order-1'}`}>
                      {!isMine && (
                        <div className="flex items-center gap-2 mb-0.5 ml-1">
                          <span className={`text-[11px] font-semibold ${msg.role === 'admin' ? 'text-violet-400' : 'text-primary-300'}`}>
                            {msg.user}
                          </span>
                          <span className="text-[10px] text-white/20">{msg.timeAgo}</span>
                        </div>
                      )}
                      {isMine && (
                        <div className="flex items-center gap-2 mb-0.5 mr-1 justify-end">
                          <span className="text-[10px] text-white/20">{msg.timeAgo}</span>
                        </div>
                      )}
                      {msg.attachmentUrl ? (
                        <a href={msg.attachmentUrl} target="_blank" rel="noopener noreferrer" className="block">
                          <img src={msg.attachmentUrl} alt="attachment" className={`max-w-48 max-h-32 rounded-xl object-cover border border-white/10 ${
                            isMine ? 'rounded-br-md' : 'rounded-bl-md'
                          }`} />
                        </a>
                      ) : (
                        <div className={`px-3.5 py-2 rounded-2xl text-sm leading-relaxed ${
                          isMine
                            ? 'bg-primary-500/25 text-white rounded-br-md'
                            : 'bg-white/5 text-white/80 rounded-bl-md'
                        }`}>
                          {msg.message}
                        </div>
                      )}
                    </div>
                  </div>
                );
              })}
              <div ref={chatEndRef} />
            </div>

            <div className="flex items-center gap-2 pt-4 border-t border-white/5 relative">
              <button onClick={() => fileInputRef.current?.click()} disabled={sendingAttachment}
                className="w-10 h-10 rounded-xl bg-white/5 hover:bg-white/10 flex items-center justify-center text-white/40 hover:text-white/60 transition-all shrink-0">
                <Paperclip size={16} />
              </button>
              <input ref={fileInputRef} type="file" accept="image/*" className="hidden" onChange={handleFileSelect} />
              <div className="flex-1 relative">
                <input
                  type="text"
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && handleSend()}
                  placeholder="Type your message..."
                  className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-2.5 pr-10 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 transition-all"
                />
                <button onClick={() => setShowEmoji(!showEmoji)}
                  className="absolute right-2.5 top-1/2 -translate-y-1/2 text-white/30 hover:text-white/60 transition-all">
                  <Smile size={16} />
                </button>
              </div>
              <button onClick={handleSend} className="w-10 h-10 rounded-xl bg-primary-500/20 hover:bg-primary-500/30 flex items-center justify-center text-primary-400 transition-all shrink-0">
                <Send size={16} />
              </button>
              <EmojiPicker
                open={showEmoji}
                onClose={() => setShowEmoji(false)}
                onSelect={(emoji) => {
                  setInput((prev) => prev + emoji);
                  setShowEmoji(false);
                }}
              />
            </div>
          </GlassCard>
        </div>

        <div>
          <GlassCard className="p-6">
            <div className="flex items-center gap-2 mb-4">
              <Users size={14} className="text-primary-400" />
              <span className="text-xs font-semibold text-white/50 uppercase tracking-wider">Online ({onlineMods.length})</span>
            </div>
            <div className="space-y-3">
              {onlineMods.map((mod) => (
                <div key={mod.name} className="flex items-center gap-3">
                  <div className={`w-2 h-2 rounded-full ${
                    mod.status === 'online' ? 'bg-emerald-500' : mod.status === 'idle' ? 'bg-amber-500' : 'bg-rose-500'
                  }`} />
                  <span className="text-sm text-white/60">{mod.name}</span>
                </div>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}