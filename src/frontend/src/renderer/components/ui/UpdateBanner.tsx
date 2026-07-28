import { useState, useEffect, useCallback } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Download, X, RefreshCw, ChevronDown, ChevronUp, AlertTriangle, Shield } from 'lucide-react';
import type { IUpdateCheckResult, IUpdateDownloadProgress } from '../../types/electron';
import { CriticalUpdateModal } from './CriticalUpdateModal';

export function UpdateBanner() {
  const [updateInfo, setUpdateInfo] = useState<IUpdateCheckResult | null>(null);
  const [dismissed, setDismissed] = useState(false);
  const [downloading, setDownloading] = useState(false);
  const [progress, setProgress] = useState<IUpdateDownloadProgress | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [showChangelog, setShowChangelog] = useState(false);

  useEffect(() => {
    if (!window.electronAPI?.checkForUpdates) return;
    let cancelled = false;
    const check = async () => {
      try {
        const result = await window.electronAPI.checkForUpdates();
        if (!cancelled) setUpdateInfo(result);
      } catch { /* ignore */ }
    };
    check();
    const interval = setInterval(check, 30 * 60 * 1000);
    return () => { cancelled = true; clearInterval(interval); };
  }, []);

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

  if (!updateInfo?.hasUpdate) return null;
  if (!updateInfo.isCritical && dismissed) return null;

  const isDownloading = downloading && progress;
  const isMandatory = updateInfo.isCritical;

  return (
    <motion.div
      initial={{ opacity: 0, height: 0 }}
      animate={{ opacity: 1, height: 'auto' }}
      exit={{ opacity: 0, height: 0 }}
      className="border-b overflow-hidden"
      style={{
        background: isMandatory
          ? 'rgba(239, 68, 68, 0.1)'
          : 'rgba(99, 102, 241, 0.08)',
        borderColor: isMandatory ? 'rgba(239, 68, 68, 0.2)' : 'rgba(255,255,255,0.05)',
      }}
    >
      <div className="px-5 py-3 flex items-center gap-4 flex-wrap">
        <div className="flex items-center gap-2.5 flex-shrink-0">
          <div
            className="w-8 h-8 rounded-lg flex items-center justify-center"
            style={{
              background: isMandatory ? 'rgba(239, 68, 68, 0.2)' : 'rgba(99, 102, 241, 0.2)',
              borderColor: isMandatory ? 'rgba(239, 68, 68, 0.3)' : 'rgba(99, 102, 241, 0.3)',
              borderWidth: 1,
            }}
          >
            {downloading ? (
              <RefreshCw size={14} className={isMandatory ? 'text-red-400 animate-spin' : 'text-primary-400 animate-spin'} />
            ) : isMandatory ? (
              <Shield size={14} className="text-red-400" />
            ) : (
              <Download size={14} className="text-primary-400" />
            )}
          </div>
          <div>
            <p className={`text-sm font-medium ${isMandatory ? 'text-red-300' : 'text-white/80'}`}>
              {downloading ? 'Downloading update...' : isMandatory ? 'Critical Update Required' : `Update ${updateInfo.latestVersion} available`}
            </p>
            <p className="text-[11px] text-white/40">
              Current: v{updateInfo.currentVersion}
              {isMandatory && (
                <span className="ml-2 text-red-400 font-medium">Required to continue</span>
              )}
            </p>
          </div>
        </div>

        {error && (
          <div className="flex items-center gap-1.5 text-[11px] text-rose-400">
            <AlertTriangle size={12} />
            {error}
          </div>
        )}

        {updateInfo.changelog && !downloading && (
          <button
            onClick={() => setShowChangelog(!showChangelog)}
            className="flex items-center gap-1 text-[11px] text-white/40 hover:text-white/60"
          >
            {showChangelog ? <ChevronUp size={12} /> : <ChevronDown size={12} />}
            What's new
          </button>
        )}

        <div className="flex-1" />

        {!downloading && (
          <button
            onClick={handleInstall}
            className="flex items-center gap-1.5 px-4 py-1.5 rounded-lg text-xs font-medium transition-colors"
            style={{
              background: isMandatory ? 'rgba(239, 68, 68, 0.2)' : 'rgba(99, 102, 241, 0.2)',
              borderColor: isMandatory ? 'rgba(239, 68, 68, 0.3)' : 'rgba(99, 102, 241, 0.3)',
              borderWidth: 1,
              color: isMandatory ? '#fca5a5' : '#a5b4fc',
            }}
            onMouseEnter={(e) => {
              e.currentTarget.style.background = isMandatory ? 'rgba(239, 68, 68, 0.3)' : 'rgba(99, 102, 241, 0.3)';
            }}
            onMouseLeave={(e) => {
              e.currentTarget.style.background = isMandatory ? 'rgba(239, 68, 68, 0.2)' : 'rgba(99, 102, 241, 0.2)';
            }}
          >
            <Download size={12} />
            {isMandatory ? 'Update Now' : 'Update Now'}
          </button>
        )}

        {isDownloading && progress && (
          <div className="flex items-center gap-2 min-w-[160px]">
            <div className="flex-1 h-1.5 rounded-full bg-white/10 overflow-hidden">
              <motion.div
                className="h-full rounded-full"
                style={{ background: isMandatory ? '#ef4444' : 'var(--color-primary-400, #818cf8)' }}
                initial={{ width: 0 }}
                animate={{ width: `${progress.percent}%` }}
                transition={{ duration: 0.3 }}
              />
            </div>
            <span className="text-[10px] text-white/40 font-mono w-10 text-right">
              {progress.percent}%
            </span>
          </div>
        )}

        {!downloading && !isMandatory && (
          <button
            onClick={() => setDismissed(true)}
            className="w-6 h-6 flex items-center justify-center rounded hover:bg-white/5 text-white/30 hover:text-white/50"
          >
            <X size={12} />
          </button>
        )}
      </div>

      <AnimatePresence>
        {showChangelog && updateInfo.changelog && (
          <motion.div
            initial={{ opacity: 0, height: 0 }}
            animate={{ opacity: 1, height: 'auto' }}
            exit={{ opacity: 0, height: 0 }}
            className="px-5 pb-3"
          >
            <div className="p-3 rounded-lg bg-white/5 border border-white/5 text-[11px] text-white/50 whitespace-pre-wrap leading-relaxed max-h-32 overflow-y-auto">
              {updateInfo.changelog}
            </div>
          </motion.div>
        )}
      </AnimatePresence>

      {isMandatory && !downloading && (
        <CriticalUpdateModal updateInfo={updateInfo} />
      )}
    </motion.div>
  );
}
