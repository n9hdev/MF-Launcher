import { useState } from 'react';
import { ShieldOff, RefreshCw, AlertTriangle } from 'lucide-react';
import { healthApi } from '../../services/health';
import { useAuthStore } from '../../stores/authStore';

export function ServiceDownPage() {
  const [restarting, setRestarting] = useState(false);
  const [restartMsg, setRestartMsg] = useState('');
  const setServiceDown = useAuthStore((s) => s.setServiceDown);

  const handleRestart = async () => {
    setRestarting(true);
    try {
      const { data } = await healthApi.restartService();
      setRestartMsg(data.message);
      setTimeout(() => window.location.reload(), 5000);
    } catch {
      setRestartMsg('Failed to contact API. Try restarting the service manually.');
    }
    setRestarting(false);
  };

  const handleRetry = async () => {
    try {
      const { data } = await healthApi.getStatus();
      if (data.healthy) {
        setServiceDown(false);
      } else {
        setRestartMsg('Service is still not responding. Try restarting.');
      }
    } catch {
      setRestartMsg('Cannot reach the API server.');
    }
  };

  return (
    <div className="h-screen w-screen flex items-center justify-center" style={{ background: 'var(--color-surface-900)' }}>
      <div className="max-w-md w-full mx-4 text-center space-y-8">
        <div className="flex justify-center">
          <div className="w-24 h-24 rounded-full bg-rose-500/10 flex items-center justify-center">
            <ShieldOff size={52} className="text-rose-400" />
          </div>
        </div>

        <div className="space-y-3">
          <h1 className="text-2xl font-bold text-white">Service Unavailable</h1>
          <p className="text-sm text-white/40 leading-relaxed">
            The anti-cheat background service is not responding. 
            This may be due to a temporary network issue or the service needs to be restarted.
          </p>
        </div>

        <div className="bg-rose-500/5 border border-rose-500/10 rounded-xl p-4 flex items-start gap-3 text-left">
          <AlertTriangle size={18} className="text-amber-400 shrink-0 mt-0.5" />
          <div className="text-sm text-white/50">
            <p className="font-medium text-white/70 mb-1">What this means:</p>
            <ul className="space-y-1 list-disc list-inside">
              <li>Ban checks and protection features are unavailable</li>
              <li>Your account is at risk without active protection</li>
              <li>All game launches are blocked until the service is restored</li>
            </ul>
          </div>
        </div>

        <div className="space-y-3">
          <button
            onClick={handleRestart}
            disabled={restarting}
            className="w-full py-3 rounded-xl bg-primary-500/20 hover:bg-primary-500/30 text-primary-400 font-medium text-sm transition-all flex items-center justify-center gap-2 disabled:opacity-50"
          >
            <RefreshCw size={16} className={restarting ? 'animate-spin' : ''} />
            {restarting ? 'Restarting...' : 'Restart Service'}
          </button>

          <button
            onClick={handleRetry}
            className="w-full py-3 rounded-xl bg-white/5 hover:bg-white/10 text-white/50 hover:text-white/70 font-medium text-sm transition-all"
          >
            Retry Connection
          </button>
        </div>

        {restartMsg && (
          <p className="text-sm text-white/30">{restartMsg}</p>
        )}
      </div>
    </div>
  );
}
