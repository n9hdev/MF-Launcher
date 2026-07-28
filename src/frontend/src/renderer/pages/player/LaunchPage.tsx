import { useEffect, useState, useCallback } from 'react';
import { motion } from 'framer-motion';
import { Gamepad2, Play, Shield, CheckCircle, AlertTriangle, XCircle, Settings, FolderOpen, Activity } from 'lucide-react';
import { useSessionStore } from '../../stores/sessionStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useAuthStore } from '../../stores/authStore';
import { gameApi } from '../../services/game';
import { authApi } from '../../services/auth';
import type { ILaunchCheck, IGameSettings, IGameStatus } from '../../services/game';
import { GlassCard } from '../../components/ui/GlassCard';
import { AnimatedButton } from '../../components/ui/AnimatedButton';

export function LaunchPage() {
  const { gameRunning, gameName, setGameRunning, stopGame } = useSessionStore();
  const { gamePath: persistedPath, setGamePath: persistPath } = useSettingsStore();
  const { gamePath: dbPath, setGamePath: setDbPath, hwidVerified, preLaunchCleared, preLaunchThreats } = useAuthStore();
  const [checks, setChecks] = useState<ILaunchCheck[]>([]);
  const [gamePath, setGamePathState] = useState(dbPath || persistedPath);
  const [settings, setSettingsState] = useState<IGameSettings>({ windowedMode: false, skipIntro: false, devConsole: false });
  const [launching, setLaunching] = useState(false);
  const [status, setStatus] = useState<IGameStatus | null>(null);

  useEffect(() => {
    refreshChecks();
    gameApi.getSettings().then(({ data }) => setSettingsState(data)).catch(() => {});
    if (!dbPath && !persistedPath) {
      gameApi.getPath().then(({ data }) => setGamePathState(data.path)).catch(() => {});
    }
    if (dbPath && !persistedPath) {
      persistPath(dbPath);
    }
  }, []);

  useEffect(() => {
    if (gamePath && gamePath !== persistedPath) {
      persistPath(gamePath);
      setDbPath(gamePath);
      authApi.updateProfile({ gamePath }).catch(() => {});
    }
  }, [gamePath]);

  useEffect(() => {
    if (!gameRunning) return;
    const interval = setInterval(async () => {
      try {
        if (window.electronAPI?.getGameStatus) {
          const data = await window.electronAPI.getGameStatus();
          const uptimeStr = data.uptime > 0 ? `${Math.floor(data.uptime / 60)}m ${data.uptime % 60}s` : undefined;
          setStatus({ isRunning: data.isRunning, startedAt: data.startedAt || undefined, uptime: uptimeStr });
          if (!data.isRunning) {
            setGameRunning(false, '', '');
          }
        } else {
          const { data } = await gameApi.getStatus();
          setStatus(data);
          if (!data.isRunning) {
            setGameRunning(false, '', '');
          }
        }
      } catch {
        setGameRunning(false, '', '');
      }
    }, 2000);
    return () => clearInterval(interval);
  }, [gameRunning]);

  const refreshChecks = useCallback(async () => {
    try {
      const { data } = await gameApi.verify();
      setChecks(data);
    } catch {}
  }, []);

  const handleLaunch = async () => {
    setLaunching(true);
    try {
      if (window.electronAPI?.launchGame) {
        const result = await window.electronAPI.launchGame(gamePath);
        if (result.success) {
          setGameRunning(true, 'MTA: San Andreas', gamePath);
        }
      } else {
        const { data } = await gameApi.launchGame(gamePath);
        if (data.success) {
          setGameRunning(true, 'MTA: San Andreas', gamePath);
        }
      }
    } catch { /* ignore */ } finally {
      setLaunching(false);
    }
  };

  const handleStop = async () => {
    try {
      if (window.electronAPI?.stopGame) {
        await window.electronAPI.stopGame();
      } else {
        await gameApi.stopGame();
      }
      stopGame();
      setStatus(null);
    } catch { /* ignore */ }
  };

  const toggleSetting = async (key: keyof IGameSettings) => {
    const updated = { ...settings, [key]: !settings[key] };
    setSettingsState(updated);
    try { await gameApi.updateSettings(updated); } catch { /* ignore */ }
  };

  const statusIcon = (s: string) => {
    switch (s) {
      case 'passed': return <CheckCircle size={10} className="text-emerald-400" />;
      case 'warning': return <AlertTriangle size={10} className="text-amber-400" />;
      case 'failed': return <XCircle size={10} className="text-rose-400" />;
      default: return <AlertTriangle size={10} className="text-white/30" />;
    }
  };

  const statusColor = (s: string) => {
    switch (s) {
      case 'passed': return 'text-emerald-400';
      case 'warning': return 'text-amber-400';
      case 'failed': return 'text-rose-400';
      default: return 'text-white/30';
    }
  };

  return (
    <div className="space-y-6">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Game Launch</h1>
        <p className="text-sm text-white/30 mt-0.5">Launch MTA:SA with full protection</p>
      </motion.div>

      <div className="grid grid-cols-3 gap-6">
        <div className="col-span-2 space-y-6">
          <GlassCard className="p-8 text-center">
            <motion.div
              animate={{ scale: [1, 1.05, 1] }}
              transition={{ duration: 3, repeat: Infinity }}
              className="w-24 h-24 rounded-3xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center mx-auto mb-6 glow-primary"
            >
              <Gamepad2 size={48} className="text-white" />
            </motion.div>
            <h2 className="text-2xl font-bold text-white mb-2">MTA: San Andreas</h2>
            <p className="text-sm text-white/30 mb-6">Multi Theft Auto &mdash; Version 1.6.0</p>

            {gameRunning ? (
              <div className="space-y-4">
                <div className="inline-flex items-center gap-2 px-4 py-2 rounded-xl bg-emerald-500/10 border border-emerald-500/20">
                  <div className="w-2 h-2 rounded-full bg-emerald-500 animate-pulse" />
                  <span className="text-sm text-emerald-400 font-medium">Game Running &mdash; {gameName}</span>
                </div>
                {status?.uptime && (
                  <p className="text-[10px] text-white/30 font-mono">
                    Uptime: {status.uptime}
                  </p>
                )}
                <AnimatedButton variant="danger" size="lg" onClick={handleStop}>Terminate Game</AnimatedButton>
              </div>
            ) : (
              <AnimatedButton variant="gradient" size="lg" icon={<Play size={18} />} fullWidth onClick={handleLaunch} loading={launching}
                locked={!preLaunchCleared}
                lockedReason={!preLaunchCleared ? (preLaunchThreats > 0 ? 'Pre-launch scan found threats — game launch blocked' : 'Running pre-launch scan...') : !hwidVerified ? 'Verify your HWID binding' : ''}
              >
                Launch Protected Game
              </AnimatedButton>
            )}

            <div className="mt-6 pt-6 border-t border-white/5">
              <p className="text-xs text-white/20">Game path: {gamePath || 'Not set'}</p>
              <button onClick={async () => {
                try {
                  const result = await window.electronAPI?.openFilePicker({ filters: [{ name: 'Executables', extensions: ['exe'] }] });
                  if (result) setGamePathState(result);
                } catch {
                  const path = prompt('Enter game executable path:', gamePath || 'C:\\Program Files\\MTA San Andreas\\MTA.exe');
                  if (path) setGamePathState(path);
                }
              }} className="text-[10px] text-primary-400 hover:text-primary-300 mt-1 flex items-center justify-center gap-1">
                <FolderOpen size={10} /> Change path
              </button>
            </div>
          </GlassCard>
        </div>

        <div className="space-y-6">
          <GlassCard className="p-6">
            <div className="flex items-center justify-between mb-4">
              <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider">Pre-Launch Checks</h3>
              <button onClick={refreshChecks} className="text-[10px] text-primary-400 hover:text-primary-300 flex items-center gap-1">
                <Activity size={10} /> Refresh
              </button>
            </div>
            <div className="space-y-3">
              {checks.map((check) => (
                <div key={check.name} className="flex items-center justify-between group" title={check.details}>
                  <span className="text-xs text-white/50">{check.name}</span>
                  <span className={`flex items-center gap-1 text-[10px] ${statusColor(check.status)}`}>
                    {statusIcon(check.status)}
                    {check.status === 'passed' ? 'Passed' : check.status === 'warning' ? 'Warning' : 'Failed'}
                  </span>
                  {check.details && (
                    <div className="absolute hidden group-hover:block right-0 top-full mt-1 bg-surface-800 border border-white/10 rounded p-2 text-[9px] text-white/50 whitespace-nowrap z-10">
                      {check.details}
                    </div>
                  )}
                </div>
              ))}
              {checks.length === 0 && (
                <p className="text-[10px] text-white/20 text-center py-2">Loading checks...</p>
              )}
            </div>
          </GlassCard>

          <GlassCard className="p-6">
            <h3 className="text-xs font-semibold text-white/50 uppercase tracking-wider mb-3">Quick Settings</h3>
            <div className="space-y-2">
              {[
                { label: 'Launch in Windowed Mode', key: 'windowedMode' as const },
                { label: 'Skip Intro Videos', key: 'skipIntro' as const },
                { label: 'Enable Dev Console', key: 'devConsole' as const },
              ].map((opt) => (
                <label key={opt.key} className="flex items-center justify-between py-2 cursor-pointer">
                  <span className="text-xs text-white/50">{opt.label}</span>
                  <div onClick={() => toggleSetting(opt.key)} className={`w-8 h-4 rounded-full relative cursor-pointer transition-colors ${settings[opt.key] ? 'bg-primary-500' : 'bg-white/10'}`}>
                    <div className={`w-3 h-3 rounded-full bg-white absolute top-0.5 transition-all ${settings[opt.key] ? 'left-4.5' : 'left-0.5'}`} />
                  </div>
                </label>
              ))}
            </div>
          </GlassCard>
        </div>
      </div>
    </div>
  );
}
