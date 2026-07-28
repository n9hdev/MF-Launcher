import { useEffect, useState } from 'react';
import { motion } from 'framer-motion';
import { useNavigate } from 'react-router-dom';
import { Shield, Gamepad2, Activity, Clock, AlertTriangle, Zap, ArrowRight, Play, CheckCircle, AlertCircle, XCircle } from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { useDetectionStore } from '../../stores/detectionStore';
import { useSessionStore } from '../../stores/sessionStore';
import { activityApi } from '../../services/activity';
import { authApi } from '../../services/auth';
import { historyApi } from '../../services/history';
import type { IActivityEvent } from '../../services/activity';
import type { TrustStatus } from '../../types/global';
import { GlassCard } from '../../components/ui/GlassCard';
import { MetricCard } from '../../components/ui/MetricCard';
import { StatusCard } from '../../components/ui/StatusCard';
import { TrustScore } from '../../components/ui/TrustScore';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { ActivityFeed } from '../../components/ui/ActivityFeed';

export function PlayerDashboard() {
  const navigate = useNavigate();
  const { user, hwidVerified, trustStatus, preLaunchCleared, preLaunchThreats } = useAuthStore();
  const { status, health, lastScanTime } = useDetectionStore();
  const { gameRunning, sessionDuration } = useSessionStore();
  const [activities, setActivities] = useState<IActivityEvent[]>([]);
  const [threatsCount, setThreatsCount] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      activityApi.getRecentActivity(5).then(({ data }) => setActivities(data)),
      historyApi.getStats().then(({ data }) => setThreatsCount(data.threatsFound)).catch((err) => console.error('[PlayerDashboard] failed to fetch', err)),
    ]).catch((err) => console.error('[PlayerDashboard] failed to fetch', err)).finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    const { setHwidVerified, setTrustStatus, setHardwareId, hardwareId: storedHwid } = useAuthStore.getState();
    const interval = setInterval(async () => {
      try {
        if (!storedHwid) {
          const hwid = await window.electronAPI?.readHwid();
          if (hwid) {
            await authApi.updateProfile({ hardwareId: hwid });
            setHardwareId(hwid);
          }
        }
        const [hwidRes, trustRes] = await Promise.all([
          authApi.verifyHardware(),
          authApi.getTrustStatus(),
        ]);
        setHwidVerified(hwidRes.data.isVerified);
        setTrustStatus(trustRes.data.trustStatus as TrustStatus);
      } catch { /* ignore polling errors */ }
    }, 10000);

    return () => clearInterval(interval);
  }, [hwidVerified, trustStatus]);

  const activeModules = Object.values(status).filter((s) => s === 'active').length;

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }} className="flex items-center justify-between">
        <div>
          <h1 className="text-xl font-bold text-white">Welcome back, <span className="text-gradient">{user?.displayName}</span></h1>
          <p className="text-sm text-white/30 mt-0.5">Your protection status &mdash; all systems nominal</p>
        </div>
        <div className="flex items-center gap-3">
          <AnimatedButton
            variant="gradient" size="lg" icon={<Play size={16} />}
            onClick={() => navigate('/player/launch')}
            locked={!preLaunchCleared}
            lockedReason={!preLaunchCleared ? (preLaunchThreats > 0 ? 'Pre-launch scan found threats — game launch blocked' : 'Pre-launch scan in progress...') : !hwidVerified ? 'Verify your HWID binding' : ''}
          >
            Launch MTA:SA
          </AnimatedButton>
        </div>
      </motion.div>

      <div className="grid grid-cols-4 gap-4">
        <MetricCard title="Trust Score" value={`${user?.trustScore || 0}%`} subtitle={user?.trustStatus === 'trusted' ? 'Trusted status' : user?.trustStatus === 'restricted' ? 'Restricted' : 'Pending verification'} trend={user?.trustStatus === 'trusted' ? 'up' : user?.trustStatus === 'restricted' ? 'down' : 'neutral'} icon={<Shield size={16} />} />
        <MetricCard title="Session" value={gameRunning ? `${Math.floor(sessionDuration / 60)}m` : 'Idle'} subtitle={gameRunning ? 'Currently in game' : 'No game running'} icon={<Clock size={16} />} />
        <MetricCard title="Detections" value={String(threatsCount)} subtitle={threatsCount === 0 ? 'All clear — no threats' : `${threatsCount} threat${threatsCount !== 1 ? 's' : ''} detected`} trend={threatsCount > 0 ? 'up' : 'down'} icon={<AlertTriangle size={16} />} />
        <MetricCard title="Modules" value={`${activeModules}/7`} subtitle="All protection active" trend="up" icon={<Activity size={16} />} />
      </div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2 space-y-6">
          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Protection Modules</h3>
              <span className="text-[10px] text-emerald-400/60">All systems nominal</span>
            </div>
            <div className="grid grid-cols-2 gap-3">
              {Object.entries(status).map(([key, value]) => (
                <StatusCard
                  key={key}
                  title={key.replace(/([A-Z])/g, ' $1').trim()}
                  status={value}
                  icon={<Zap size={16} />}
                />
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Activity</h3>
              <button onClick={() => navigate('/player/history')} className="text-[10px] text-primary-400 hover:text-primary-300 flex items-center gap-1">View all <ArrowRight size={10} /></button>
            </div>
            {loading ? (
              <div className="space-y-2"><div className="h-10 bg-white/5 rounded-lg animate-pulse" /><div className="h-10 bg-white/5 rounded-lg animate-pulse" /></div>
            ) : (
              <ActivityFeed activities={activities.map((a) => {
                let type: 'scan' | 'user' | 'alert' | 'system' | 'session' = 'scan';
                if (a.severity === 'high' || a.severity === 'alert') type = 'alert';
                else if (a.type === 'achievement') type = 'user';
                else if (a.type === 'game') type = 'session';
                else if (a.type === 'system') type = 'system';
                return { id: a.id, type, title: a.title, description: a.description, timestamp: new Date(a.timestamp).toLocaleString() };
              })} />
            )}
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6 flex flex-col items-center">
            <TrustScore score={user?.trustScore || 85} size="lg" />
            {user?.trustStatus && (
              <div className={`mt-3 flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-medium ${
                user.trustStatus === 'trusted' ? 'bg-emerald-500/10 text-emerald-400 border border-emerald-500/20'
                  : user.trustStatus === 'restricted' ? 'bg-rose-500/10 text-rose-400 border border-rose-500/20'
                  : 'bg-amber-500/10 text-amber-400 border border-amber-500/20'
              }`}>
                {user.trustStatus === 'trusted' ? <CheckCircle size={12} />
                  : user.trustStatus === 'restricted' ? <XCircle size={12} />
                  : <AlertCircle size={12} />}
                {user.trustStatus === 'trusted' ? 'Trusted'
                  : user.trustStatus === 'restricted' ? 'Restricted'
                  : 'Pending Verification'}
              </div>
            )}
            <div className="mt-4 text-center">
              <p className="text-xs text-white/30">Protection Level</p>
              <p className="text-lg font-bold text-gradient">Premium</p>
            </div>
            <div className="w-full mt-4 pt-4 border-t border-white/5 space-y-2">
              {[
                { label: 'Level', value: user?.level || 42 },
                { label: 'XP Progress', value: `${user?.xp || 3400}/${user?.nextLevelXp || 5000}` },
                { label: 'Last Scan', value: lastScanTime || 'Just now' },
              ].map((item) => (
                <div key={item.label} className="flex items-center justify-between text-xs">
                  <span className="text-white/30">{item.label}</span>
                  <span className="text-white/60 font-mono">{item.value}</span>
                </div>
              ))}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Quick Actions</h3>
            <div className="space-y-2">
              {[
                { label: 'Run Full Scan', icon: <Activity size={14} />, onClick: () => navigate('/player/protection') },
                { label: 'Report Player', icon: <AlertTriangle size={14} />, onClick: () => navigate('/player/reports') },
                { label: 'View History', icon: <Clock size={14} />, onClick: () => navigate('/player/history') },
              ].map((action) => (
                <button key={action.label} onClick={action.onClick} className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs text-white/50 hover:text-white/80 hover:bg-white/5 transition-all">
                  {action.icon}
                  {action.label}
                </button>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
