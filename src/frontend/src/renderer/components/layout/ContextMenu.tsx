import { useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { useUIStore } from '../../stores/uiStore';

export function ContextMenu() {
  const { contextMenu, closeContextMenu } = useUIStore();

  useEffect(() => {
    if (!contextMenu) return;
    const handler = () => closeContextMenu();
    document.addEventListener('click', handler);
    return () => document.removeEventListener('click', handler);
  }, [contextMenu, closeContextMenu]);

  return (
    <AnimatePresence>
      {contextMenu && (
        <motion.div
          initial={{ opacity: 0, scale: 0.95 }}
          animate={{ opacity: 1, scale: 1 }}
          exit={{ opacity: 0, scale: 0.95 }}
          style={{ left: contextMenu.x, top: contextMenu.y, position: 'fixed', zIndex: 100, background: 'rgba(15, 23, 42, 0.95)', backdropFilter: 'blur(24px) saturate(1.6)', border: '1px solid rgba(255,255,255,0.08)' }}
          className="min-w-[180px] rounded-xl overflow-hidden py-1"
        >
          {contextMenu.items.map((item, i) => (
            <button
              key={i}
              onClick={() => { item.action(); closeContextMenu(); }}
              className="w-full flex items-center gap-3 px-4 py-2 text-xs text-white/50 hover:text-white/80 hover:bg-white/5 transition-colors"
            >
              {item.label}
            </button>
          ))}
        </motion.div>
      )}
    </AnimatePresence>
  );
}
