import { motion, AnimatePresence } from 'framer-motion';
import { X } from 'lucide-react';
import { useUIStore } from '../../stores/uiStore';

export function InfoDrawer() {
  const { infoDrawerOpen, infoDrawerContent, setInfoDrawerContent } = useUIStore();

  return (
    <AnimatePresence>
      {infoDrawerOpen && (
        <>
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 bg-black/20 z-40"
            onClick={() => setInfoDrawerContent(null)}
          />
          <motion.aside
            initial={{ x: 320 }}
            animate={{ x: 0 }}
            exit={{ x: 320 }}
            transition={{ type: 'spring', stiffness: 300, damping: 30 }}
            className="fixed right-0 top-0 h-full w-80 z-50 overflow-y-auto"
            style={{ background: 'rgba(15, 23, 42, 0.95)', backdropFilter: 'blur(24px) saturate(1.6)', borderLeft: '1px solid rgba(255,255,255,0.06)' }}
          >
            <div className="flex items-center justify-between p-4 border-b border-white/5">
              <span className="text-sm font-semibold text-white/80">Details</span>
              <button
                onClick={() => setInfoDrawerContent(null)}
                className="w-7 h-7 flex items-center justify-center rounded-lg hover:bg-white/5 text-white/30 hover:text-white/60"
              >
                <X size={14} />
              </button>
            </div>
            <div className="p-4">
              {infoDrawerContent}
            </div>
          </motion.aside>
        </>
      )}
    </AnimatePresence>
  );
}
