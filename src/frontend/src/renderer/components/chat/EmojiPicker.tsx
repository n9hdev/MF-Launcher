import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Smile } from 'lucide-react';
import { EMOJI_CATEGORIES } from './emojiData';
import type { IEmojiCategory } from './emojiData';

interface IEmojiPickerProps {
  onSelect: (emoji: string) => void;
  open: boolean;
  onClose: () => void;
}

export function EmojiPicker({ onSelect, open, onClose }: IEmojiPickerProps) {
  const [activeCat, setActiveCat] = useState(EMOJI_CATEGORIES[0]?.id || '');

  const activeCategory = EMOJI_CATEGORIES.find((c) => c.id === activeCat);

  return (
    <AnimatePresence>
      {open && (
        <>
          <div className="fixed inset-0 z-40" onClick={onClose} />
          <motion.div
            initial={{ opacity: 0, scale: 0.9, y: 8 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.9, y: 8 }}
            transition={{ duration: 0.15 }}
            className="absolute bottom-14 left-0 z-50 w-[340px] h-72 rounded-xl flex flex-col overflow-hidden"
            style={{ background: 'rgba(15, 23, 42, 0.98)', border: '1px solid rgba(255,255,255,0.08)', backdropFilter: 'blur(24px) saturate(1.6)' }}
          >
            {/* Category tabs */}
            <div className="flex items-center gap-0.5 px-2 pt-2 pb-1 border-b border-white/5 overflow-x-auto scrollbar-hide">
              {EMOJI_CATEGORIES.map((cat) => (
                <button
                  key={cat.id}
                  type="button"
                  onClick={() => setActiveCat(cat.id)}
                  className={`flex items-center justify-center w-9 h-8 rounded-lg text-sm transition-colors shrink-0 ${
                    activeCat === cat.id
                      ? 'bg-primary-500/15 text-primary-300'
                      : 'text-white/40 hover:text-white/60 hover:bg-white/5'
                  }`}
                  title={cat.label}
                >
                  {cat.icon}
                </button>
              ))}
            </div>

            {/* Emoji grid */}
            <div className="flex-1 overflow-y-auto p-2 scrollbar-hide">
              {activeCategory && (
                <div className="grid grid-cols-9 gap-0.5">
                  {activeCategory.emojis.map((emoji, i) => (
                    <button
                      key={`${emoji}-${i}`}
                      type="button"
                      onClick={() => { onSelect(emoji); onClose(); }}
                      className="w-8 h-8 flex items-center justify-center rounded-md hover:bg-white/10 text-lg transition-colors"
                    >
                      {emoji}
                    </button>
                  ))}
                </div>
              )}
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
