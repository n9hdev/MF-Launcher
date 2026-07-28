import { useState, useEffect, useCallback } from 'react';
import { motion } from 'framer-motion';
import { Settings, Bell, Monitor, Lock, Shield, LogOut, User, Palette, Key, RefreshCw, Globe, Fingerprint, Copy, Check, Eye, EyeOff } from 'lucide-react';
import { GlassCard } from '../../components/ui/GlassCard';
import { Select } from '../../components/ui/Select';
import { AnimatedButton } from '../../components/ui/AnimatedButton';
import { useAuthStore } from '../../stores/authStore';
import { useSettingsStore } from '../../stores/settingsStore';
import { useTheme } from '../../theme/ThemeProvider';
import { authApi } from '../../services/auth';

const tabs = [
  { id: 'general', label: 'General', icon: <Settings size={14} /> },
  { id: 'appearance', label: 'Appearance', icon: <Palette size={14} /> },
  { id: 'notifications', label: 'Notifications', icon: <Bell size={14} /> },
  { id: 'security', label: 'Security', icon: <Lock size={14} /> },
  { id: 'account', label: 'Account', icon: <User size={14} /> },
];

function CopyBtn({ text }: { text: string }) {
  const [copied, setCopied] = useState(false);
  const handle = useCallback(async () => {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {}
  }, [text]);
  return (
    <button onClick={handle} className="text-white/20 hover:text-primary-400 transition-all shrink-0" title="Click to copy">
      {copied ? <Check size={12} className="text-emerald-400" /> : <Copy size={12} />}
    </button>
  );
}

export function SettingsPage() {
  const [activeTab, setActiveTab] = useState('general');
  const { user, logout } = useAuthStore();
  const { mode, toggleMode } = useTheme();
  const store = useSettingsStore();
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [pwError, setPwError] = useState('');
  const [pwSuccess, setPwSuccess] = useState(false);
  const [pwLoading, setPwLoading] = useState(false);
  const [showCur, setShowCur] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [identity, setIdentity] = useState<{ ip: string; hardwareId: string; serialNumber: string } | null>(null);
  const [identityLoading, setIdentityLoading] = useState(false);

  useEffect(() => {
    if (activeTab === 'account') {
      setIdentityLoading(true);
      authApi.getIdentity()
        .then(({ data }) => setIdentity(data))
        .catch(() => {})
        .finally(() => setIdentityLoading(false));
    }
  }, [activeTab]);

  const handleChangePassword = async () => {
    setPwError('');
    setPwSuccess(false);
    if (!currentPassword || !newPassword || !confirmPassword) {
      setPwError('All fields are required');
      return;
    }
    if (newPassword.length < 6) {
      setPwError('New password must be at least 6 characters');
      return;
    }
    if (newPassword !== confirmPassword) {
      setPwError('Passwords do not match');
      return;
    }
    setPwLoading(true);
    try {
      await authApi.changePassword(currentPassword, newPassword);
      setPwSuccess(true);
      setCurrentPassword('');
      setNewPassword('');
      setConfirmPassword('');
    } catch {
      setPwError('Current password is incorrect');
    }
    setPwLoading(false);
  };

  const toggleRow = (label: string, value: boolean, setter: (v: boolean) => void) => (
    <div key={label} className="flex items-center justify-between py-3 border-b border-white/5 last:border-0">
      <div>
        <span className="text-sm text-white/70">{label}</span>
      </div>
      <button
        onClick={() => setter(!value)}
        className={`w-10 h-5 rounded-full transition-all duration-300 relative ${value ? 'bg-primary-500' : 'bg-white/10'}`}
      >
        <div className={`w-4 h-4 rounded-full bg-white absolute top-0.5 transition-all duration-300 ${value ? 'left-5' : 'left-0.5'}`} />
      </button>
    </div>
  );

  return (
    <div className="space-y-6 max-w-4xl">
      <motion.div initial={{ opacity: 0, y: -10 }} animate={{ opacity: 1, y: 0 }}>
        <h1 className="text-xl font-bold text-white">Settings</h1>
        <p className="text-sm text-white/30 mt-0.5">Configure your anti-cheat experience</p>
      </motion.div>

      <div className="flex gap-6">
        <div className="w-48 flex-shrink-0 space-y-1">
          {tabs.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs transition-all ${
                activeTab === tab.id ? 'bg-primary-500/15 text-primary-300 border border-primary-500/20' : 'text-white/40 hover:text-white/70 hover:bg-white/5'
              }`}
            >
              {tab.icon}
              {tab.label}
            </button>
          ))}
          <div className="pt-4 mt-4 border-t border-white/5">
            <button
              onClick={logout}
              className="w-full flex items-center gap-3 px-3 py-2.5 rounded-lg text-xs text-rose-400/70 hover:text-rose-400 hover:bg-rose-500/5 transition-all"
            >
              <LogOut size={14} /> Sign Out
            </button>
          </div>
        </div>

        <div className="flex-1">
          {activeTab === 'general' && (
            <GlassCard className="p-6">
              <h3 className="text-sm font-semibold text-white/80 mb-4">General Settings</h3>
              <div className="space-y-1">
                {toggleRow('Minimize to System Tray', store.minimizeToTray, store.setMinimizeToTray)}
                {toggleRow('Start with Windows', store.startOnBoot, store.setStartOnBoot)}
                {toggleRow('Auto-scan on Launch', store.autoScan, store.setAutoScan)}
                {toggleRow('Show FPS Counter', store.showFps, store.setShowFps)}
              </div>
              <div className="mt-4 pt-4 border-t border-white/5">
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-2">Scan Interval (seconds)</label>
                <input
                  type="range"
                  min="10"
                  max="300"
                  value={store.scanInterval}
                  onChange={(e) => store.setScanInterval(Number(e.target.value))}
                  className="w-full"
                />
                <span className="text-xs text-white/50 font-mono">{store.scanInterval}s</span>
              </div>
            </GlassCard>
          )}

          {activeTab === 'appearance' && (
            <GlassCard className="p-6">
              <h3 className="text-sm font-semibold text-white/80 mb-4">Appearance</h3>
              <div className="space-y-4">
                <div className="flex items-center justify-between py-3">
                  <div>
                    <span className="text-sm text-white/70">Theme</span>
                    <p className="text-[10px] text-white/30 mt-0.5">Current: {mode === 'dark' ? 'Dark' : 'High Contrast'}</p>
                  </div>
                  <AnimatedButton size="sm" variant="secondary" onClick={toggleMode}>
                    Switch to {mode === 'dark' ? 'High Contrast' : 'Dark'}
                  </AnimatedButton>
                </div>
                {toggleRow('Reduced Motion', store.reducedMotion, store.setReducedMotion)}
                {toggleRow('Compact Mode', store.compactMode, store.setCompactMode)}
                <div>
                  <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-2">Language</label>
                  <Select value={store.language} onChange={(e) => store.setLanguage(e.target.value)}>
                    <option value="en">English</option>
                    <option value="es">Español</option>
                    <option value="fr">Français</option>
                    <option value="de">Deutsch</option>
                  </Select>
                </div>
              </div>
            </GlassCard>
          )}

          {activeTab === 'notifications' && (
            <GlassCard className="p-6">
              <h3 className="text-sm font-semibold text-white/80 mb-4">Notification Settings</h3>
              <div className="space-y-1">
                {toggleRow('Desktop Notifications', store.notifications, store.setNotifications)}
                {toggleRow('Detection Alerts', store.detectionAlerts, store.setDetectionAlerts)}
                {toggleRow('Achievement Notifications', store.achievementNotifications, store.setAchievementNotifications)}
                {toggleRow('Sound Effects', store.soundEffects, store.setSoundEffects)}
              </div>
            </GlassCard>
          )}

          {activeTab === 'security' && (
            <GlassCard className="p-6">
              <h3 className="text-sm font-semibold text-white/80 mb-4">Security Settings</h3>
              <div className="space-y-4">
                {toggleRow('Enable Kernel-Level Protection', store.kernelLevelProtection, store.setKernelLevelProtection)}
                {toggleRow('Submit Telemetry', store.submitTelemetry, store.setSubmitTelemetry)}
                {toggleRow('Automatic Updates', store.automaticUpdates, store.setAutomaticUpdates)}
                <div className="pt-4 border-t border-white/5">
                  <AnimatedButton variant="secondary" icon={<RefreshCw size={12} />} onClick={() => {
                    if (window.confirm('Are you sure you want to reset all settings to defaults?')) {
                      store.setMinimizeToTray(true);
                      store.setStartOnBoot(false);
                      store.setAutoScan(true);
                      store.setShowFps(false);
                      store.setScanInterval(30);
                      store.setReducedMotion(false);
                      store.setCompactMode(false);
                      store.setNotifications(true);
                      store.setDetectionAlerts(true);
                      store.setAchievementNotifications(true);
                      store.setSoundEffects(true);
                      store.setKernelLevelProtection(true);
                      store.setSubmitTelemetry(true);
                      store.setAutomaticUpdates(true);
                    }
                  }}>Reset to Defaults</AnimatedButton>
                </div>
              </div>
            </GlassCard>
          )}

          {activeTab === 'account' && (
            <div className="space-y-6">
              <GlassCard className="p-6">
                <h3 className="text-sm font-semibold text-white/80 mb-4">Account</h3>
                <div className="flex items-center gap-4 pb-4 border-b border-white/5 mb-4">
                  <div className="w-14 h-14 rounded-2xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center text-xl font-bold text-white">
                    {user?.displayName?.charAt(0) || 'U'}
                  </div>
                  <div>
                    <p className="text-sm font-semibold text-white/80">{user?.displayName}</p>
                    <p className="text-xs text-white/40">@{user?.username}</p>
                    <p className="text-[10px] text-white/20 mt-0.5 capitalize">Role: {user?.role}</p>
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4 text-sm">
                  <div>
                    <span className="text-[10px] text-white/30 uppercase tracking-wider">Trust Score</span>
                    <p className="text-white/80 mt-0.5">{user?.trustScore}%</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-white/30 uppercase tracking-wider">Level</span>
                    <p className="text-white/80 mt-0.5">{user?.level}</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-white/30 uppercase tracking-wider">Created</span>
                    <p className="text-white/80 mt-0.5">{user?.createdAt ? new Date(user.createdAt).toLocaleDateString() : '—'}</p>
                  </div>
                  <div>
                    <span className="text-[10px] text-white/30 uppercase tracking-wider">Last Login</span>
                    <p className="text-white/80 mt-0.5">{user?.lastLogin ? new Date(user.lastLogin).toLocaleString() : '—'}</p>
                  </div>
                </div>
              </GlassCard>

              <GlassCard className="p-6">
                <h3 className="text-sm font-semibold text-white/80 mb-4 flex items-center gap-2"><Key size={14} />Change Password</h3>
                <div className="space-y-3 max-w-sm">
                  <div className="relative">
                    <input
                      type={showCur ? 'text' : 'password'}
                      value={currentPassword}
                      onChange={(e) => setCurrentPassword(e.target.value)}
                      placeholder="Current password"
                      className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-2 pr-8 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 transition-all"
                    />
                    <button onClick={() => setShowCur(!showCur)} className="absolute right-2.5 top-1/2 -translate-y-1/2 text-white/20 hover:text-white/40">
                      {showCur ? <EyeOff size={14} /> : <Eye size={14} />}
                    </button>
                  </div>
                  <div className="relative">
                    <input
                      type={showNew ? 'text' : 'password'}
                      value={newPassword}
                      onChange={(e) => setNewPassword(e.target.value)}
                      placeholder="New password"
                      className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-2 pr-8 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 transition-all"
                    />
                    <button onClick={() => setShowNew(!showNew)} className="absolute right-2.5 top-1/2 -translate-y-1/2 text-white/20 hover:text-white/40">
                      {showNew ? <EyeOff size={14} /> : <Eye size={14} />}
                    </button>
                  </div>
                  <div className="relative">
                    <input
                      type={showConfirm ? 'text' : 'password'}
                      value={confirmPassword}
                      onChange={(e) => setConfirmPassword(e.target.value)}
                      placeholder="Confirm new password"
                      className="w-full bg-white/5 border border-white/10 rounded-lg px-3 py-2 pr-8 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 transition-all"
                    />
                    <button onClick={() => setShowConfirm(!showConfirm)} className="absolute right-2.5 top-1/2 -translate-y-1/2 text-white/20 hover:text-white/40">
                      {showConfirm ? <EyeOff size={14} /> : <Eye size={14} />}
                    </button>
                  </div>
                  {pwError && <p className="text-xs text-rose-400">{pwError}</p>}
                  {pwSuccess && <p className="text-xs text-emerald-400">Password changed successfully</p>}
                  <AnimatedButton variant="gradient" size="sm" onClick={handleChangePassword} loading={pwLoading}>Update Password</AnimatedButton>
                </div>
              </GlassCard>

              <GlassCard className="p-6">
                <h3 className="text-sm font-semibold text-white/80 mb-4 flex items-center gap-2"><Shield size={14} />Identity & Fingerprint</h3>
                {identityLoading ? (
                  <div className="h-16 bg-white/5 rounded-lg animate-pulse" />
                ) : identity ? (
                  <div className="grid grid-cols-3 gap-4">
                    <div className="p-3 rounded-lg bg-white/[0.03] border border-white/5 space-y-1">
                      <div className="flex items-center gap-1.5 text-[10px] text-white/30 uppercase tracking-wider"><Globe size={12} />IP Address</div>
                      <div className="flex items-center gap-2">
                        <p className="text-xs font-mono text-white/70 truncate">{identity.ip}</p>
                        <CopyBtn text={identity.ip} />
                      </div>
                    </div>
                    <div className="p-3 rounded-lg bg-white/[0.03] border border-white/5 space-y-1">
                      <div className="flex items-center gap-1.5 text-[10px] text-white/30 uppercase tracking-wider"><Monitor size={12} />Hardware ID</div>
                      <div className="flex items-center gap-2">
                        <p className="text-xs font-mono text-white/70 break-all truncate">{identity.hardwareId}</p>
                        <CopyBtn text={identity.hardwareId} />
                      </div>
                    </div>
                    <div className="p-3 rounded-lg bg-white/[0.03] border border-white/5 space-y-1">
                      <div className="flex items-center gap-1.5 text-[10px] text-white/30 uppercase tracking-wider"><Fingerprint size={12} />Serial Number</div>
                      <div className="flex items-center gap-2">
                        <p className="text-xs font-mono text-white/70 break-all truncate">{identity.serialNumber}</p>
                        <CopyBtn text={identity.serialNumber} />
                      </div>
                    </div>
                  </div>
                ) : (
                  <p className="text-xs text-white/20">No identity data available</p>
                )}
              </GlassCard>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
