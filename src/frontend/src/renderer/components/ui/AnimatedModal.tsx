import { motion, AnimatePresence } from 'framer-motion';
import { X } from 'lucide-react';

interface IAnimatedModalProps {
  open: boolean;
  onClose: () => void;
  title?: string;
  children: React.ReactNode;
  width?: string;
}

export function AnimatedModal({ open, onClose, title, children, width = 'max-w-lg' }: IAnimatedModalProps) {
  return (
    <AnimatePresence>
      {open && (
        <>
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/40 z-50"
            onClick={onClose}
          />
          <motion.div
            initial={{ opacity: 0, scale: 0.95, y: 20 }}
            animate={{ opacity: 1, scale: 1, y: 0 }}
            exit={{ opacity: 0, scale: 0.95, y: 20 }}
            transition={{ type: 'spring', stiffness: 300, damping: 25 }}
            className={`fixed inset-0 flex items-center justify-center z-50`}
          >
            <div
              className={`w-full ${width} rounded-xl overflow-hidden max-h-[85vh] overflow-y-auto`}
              style={{ background: 'rgba(15, 23, 42, 0.98)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
            >
              {title && (
                <div className="flex items-center justify-between px-5 py-4 border-b border-white/5">
                  <h3 className="text-sm font-semibold text-white/80">{title}</h3>
                  <button onClick={onClose} className="w-7 h-7 flex items-center justify-center rounded-lg hover:bg-white/5 text-white/30 hover:text-white/60">
                    <X size={14} />
                  </button>
                </div>
              )}
              <div className="p-5">
                {children}
              </div>
            </div>
          </motion.div>
        </>
      )}
    </AnimatePresence>
  );
}
