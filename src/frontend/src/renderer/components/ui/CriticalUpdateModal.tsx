import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Download, Shield, AlertTriangle, RefreshCw } from 'lucide-react';
import type { IUpdateCheckResult, IUpdateDownloadProgress } from '../../types/electron';

interface CriticalUpdateModalProps {
  updateInfo: IUpdateCheckResult;
  onDismiss?: () => void;
}

export function CriticalUpdateModal({ updateInfo, onDismiss }: CriticalUpdateModalProps) {
  const [downloading, setDownloading] = useState(false);
  const [progress, setProgress] = useState<IUpdateDownloadProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showChangelog, setShowChangelog] = useState(false);

  useEffect(() => {
    if (!window.electronAPI?.onUpdateDownloadProgress) return;
    window.electronAPI.onUpdateDownloadProgress((p: IUpdateDownloadProgress) => {
      setProgress(p);
    });
  }, []);

  const handleInstall = useCallback(async () => {
    if (!updateInfo?.downloadUrl) return;
    setDownloading(true);
    setError(null);
    try {
      const result = await window.electronAPI.installUpdate(
        updateInfo.downloadUrl,
        updateInfo.fallbackDownloadUrl || '',
        updateInfo.sha256,
        updateInfo.size,
      );
      if (!result.success) {
        setError(result.error || 'Installation failed');
        setDownloading(false);
      }
    } catch (err: any) {
      setError(err.message || 'Installation failed');
      setDownloading(false);
    }
  }, [updateInfo]);

  const isDownloading = downloading && progress;

  return (
    <div
      className="fixed inset-0 z-[100] flex items-center justify-center"
      style={{ background: 'rgba(0, 0, 0, 0.7)', backdropFilter: 'blur(8px)' }}
    >
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        className="w-full max-w-md mx-4 rounded-2xl overflow-hidden border"
        style={{
          background: 'rgba(15, 23, 42, 0.97)',
          backdropFilter: 'blur(24px) saturate(1.6)',
          borderColor: 'rgba(239, 68, 68, 0.3)',
        }}
      >
        <div className="px-6 pt-6 pb-4 flex flex-col items-center text-center gap-4">
          <div
            className="w-16 h-16 rounded-2xl flex items-center justify-center"
            style={{
              background: 'rgba(239, 68, 68, 0.15)',
              border: '1px solid rgba(239, 68, 68, 0.3)',
            }}
          >
            {downloading ? (
              <RefreshCw size={28} className="text-red-400 animate-spin" />
            ) : (
              <Shield size={28} className="text-red-400" />
            )}
          </div>

          <div>
            <h2 className="text-lg font-bold text-white/90">Critical Update Required</h2>
            <p className="text-sm text-white/50 mt-1 max-w-sm">
              Version <span className="text-red-400 font-medium">{updateInfo.latestVersion}</span> is required to continue using protection features.
              Please update now to maintain your security.
            </p>
          </div>

          {error && (
            <div className="flex items-center gap-2 px-3 py-2 rounded-lg text-xs" style={{ background: 'rgba(239, 68, 68, 0.1)', border: '1px solid rgba(239, 68, 68, 0.2)' }}>
              <AlertTriangle size={12} className="text-red-400 flex-shrink-0" />
              <span className="text-red-300">{error}</span>
            </div>
          )}

          {updateInfo.changelog && !downloading && (
            <button
              onClick={() => setShowChangelog(!showChangelog)}
              className="text-xs text-white/40 hover:text-white/60 underline underline-offset-2"
            >
              {showChangelog ? 'Hide changelog' : 'View changelog'}
            </button>
          )}

          <AnimatePresence>
            {showChangelog && (
              <motion.div
                initial={{ opacity: 0, height: 0 }}
                animate={{ opacity: 1, height: 'auto' }}
                exit={{ opacity: 0, height: 0 }}
                className="w-full"
              >
                <div
                  className="p-3 rounded-lg text-[11px] text-white/50 whitespace-pre-wrap leading-relaxed max-h-24 overflow-y-auto text-left"
                  style={{ background: 'rgba(255,255,255,0.05)', border: '1px solid rgba(255,255,255,0.08)' }}
                >
                  {updateInfo.changelog}
                </div>
              </motion.div>
            )}
          </AnimatePresence>

          {isDownloading && progress && (
            <div className="w-full flex items-center gap-3">
              <div className="flex-1 h-2 rounded-full" style={{ background: 'rgba(255,255,255,0.1)' }}>
                <motion.div
                  className="h-full rounded-full bg-red-500"
                  initial={{ width: 0 }}
                  animate={{ width: `${progress.percent}%` }}
                  transition={{ duration: 0.3 }}
                />
              </div>
              <span className="text-xs text-white/40 font-mono">{progress.percent}%</span>
            </div>
          )}

          <div className="flex gap-3 w-full mt-2">
            {!downloading && (
              <button
                onClick={handleInstall}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl text-sm font-semibold text-white transition-all"
                style={{
                  background: 'linear-gradient(135deg, #ef4444, #dc2626)',
                  boxShadow: '0 4px 20px rgba(239, 68, 68, 0.3)',
                }}
                onMouseEnter={(e) => {
                  e.currentTarget.style.boxShadow = '0 6px 28px rgba(239, 68, 68, 0.4)';
                }}
                onMouseLeave={(e) => {
                  e.currentTarget.style.boxShadow = '0 4px 20px rgba(239, 68, 68, 0.3)';
                }}
              >
                <Download size={16} />
                Install Update Now
              </button>
            )}
          </div>

          <p className="text-[10px] text-white/25">
            Your current version: v{updateInfo.currentVersion}
            {updateInfo.size > 0 && ` · ${(updateInfo.size / 1024 / 1024).toFixed(1)} MB`}
          </p>
        </div>
      </motion.div>
    </div>
  );
}
