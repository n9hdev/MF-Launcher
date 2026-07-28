import { useState } from 'react';
import { motion } from 'framer-motion';
import { Shield, Eye, EyeOff, AlertCircle, Gamepad2, Users, Swords } from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { useUIStore } from '../../stores/uiStore';
import { authApi } from '../../services/auth';
import { getDeviceInfo, generateDeviceId } from '../../utils/deviceFingerprint';

const features = [
  { icon: Shield, label: 'Kernel-Level Protection', desc: 'Real-time threat detection' },
  { icon: Gamepad2, label: 'Game Integration', desc: 'Seamless MTA:SA support' },
  { icon: Users, label: 'Community Driven', desc: '100K+ protected players' },
  { icon: Swords, label: 'Anti-Cheat Shield', desc: '99.7% detection rate' },
];

export function LoginPage() {
  const [mode, setMode] = useState<'login' | 'register'>('login');
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [displayName, setDisplayName] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(() => localStorage.getItem('ac-remember') !== 'false');
  const [registerError, setRegisterError] = useState<string | null>(null);
  const { setAuth, setLoggingIn, setLoginError, isLoggingIn, loginError } = useAuthStore();

  const resetForm = () => {
    setUsername('');
    setPassword('');
    setDisplayName('');
    setLoginError(null);
    setRegisterError(null);
  };

  const toggleMode = () => {
    setMode(m => m === 'login' ? 'register' : 'login');
    resetForm();
  };

  const addToast = useUIStore((s) => s.addToast);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoginError(null);

    if (!username.trim() || !password.trim()) {
      setLoginError('Please enter username and password');
      return;
    }

    setLoggingIn(true);

    try {
      const { data } = await authApi.login({
        username: username.trim(),
        password,
        deviceId: generateDeviceId(),
      });

      if (data.isBanned) {
        const reason = data.banInfo?.reason ? `: ${data.banInfo.reason}` : '';
        addToast({ type: 'error', title: 'You have been banned', message: `Your account is banned${reason}`, duration: 6000 });
      }

      setAuth(
        {
          id: data.user.id,
          username: data.user.username,
          displayName: data.user.displayName || data.user.username,
          role: data.user.role as 'player' | 'moderator' | 'admin' | 'superadmin',
          trustScore: data.user.trustScore,
          trustStatus: (data.trustStatus || data.user.trustStatus || 'pending') as 'trusted' | 'pending' | 'restricted',
          level: data.user.level,
          xp: data.user.xp || 0,
          nextLevelXp: data.user.nextLevelXp || 5000,
          status: 'online',
          createdAt: data.user.createdAt || new Date().toISOString(),
          lastLogin: new Date().toISOString(),
          badges: [],
          email: data.user.email,
          avatar: data.user.avatar,
        },
        data.accessToken,
        data.refreshToken,
        data.sessionId,
        {
          trustStatus: (data.trustStatus || 'pending') as 'trusted' | 'pending' | 'restricted',
          hwidVerified: data.hwidVerified ?? false,
          isBanned: data.isBanned ?? false,
          banInfo: data.banInfo ?? null,
        }
      );

      const deviceInfo = getDeviceInfo();
      authApi.registerDevice(deviceInfo).catch((err) => console.error('[LoginPage] failed to fetch', err));

      window.electronAPI?.writeSessionOwner(data.user.id);
      linkHardwareId();
    } catch (error: unknown) {
      if (error && typeof error === 'object' && 'response' in error) {
        const axiosErr = error as { response?: { data?: { error?: string; message?: string } } };
        setLoginError(axiosErr.response?.data?.error || axiosErr.response?.data?.message || 'Invalid credentials');
      } else if (error && typeof error === 'object' && 'request' in error) {
        setLoginError('Cannot reach server - check your connection');
      } else {
        setLoginError('Invalid credentials');
      }
    } finally {
      setLoggingIn(false);
    }
  };

  const linkHardwareId = async () => {
    try {
      const hwid = await window.electronAPI?.readHwid();
      if (hwid) {
        await authApi.updateProfile({ hardwareId: hwid });
        useAuthStore.getState().setHardwareId(hwid);
      }
    } catch {}
  };

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    setRegisterError(null);

    if (!username.trim() || !password.trim()) {
      setRegisterError('Please enter username and password');
      return;
    }

    if (password.length < 4) {
      setRegisterError('Password must be at least 4 characters');
      return;
    }

    setLoggingIn(true);

    try {
      const regHwid = await window.electronAPI?.readHwid() || undefined;
      const { data } = await authApi.register({
        username: username.trim(),
        password,
        displayName: displayName.trim() || username.trim(),
        hardwareId: regHwid,
      });

      setAuth(
        {
          id: data.user.id,
          username: data.user.username,
          displayName: data.user.displayName || data.user.username,
          role: data.user.role as 'player' | 'moderator' | 'admin' | 'superadmin',
          trustScore: data.user.trustScore,
          trustStatus: (data.trustStatus || data.user.trustStatus || 'pending') as 'trusted' | 'pending' | 'restricted',
          level: data.user.level,
          xp: data.user.xp || 0,
          nextLevelXp: data.user.nextLevelXp || 5000,
          status: 'online',
          createdAt: data.user.createdAt || new Date().toISOString(),
          lastLogin: new Date().toISOString(),
          badges: [],
          email: data.user.email,
          avatar: data.user.avatar,
        },
        data.accessToken,
        data.refreshToken,
        data.sessionId,
        {
          trustStatus: (data.trustStatus || 'pending') as 'trusted' | 'pending' | 'restricted',
          hwidVerified: data.hwidVerified ?? false,
          isBanned: data.isBanned ?? false,
          banInfo: data.banInfo ?? null,
        }
      );
      const deviceInfo = getDeviceInfo();
      authApi.registerDevice(deviceInfo).catch((err) => console.error('[LoginPage] failed to fetch', err));

      window.electronAPI?.writeSessionOwner(data.user.id);
      linkHardwareId();
    } catch (err: unknown) {
      if (err && typeof err === 'object' && 'response' in err) {
        const axiosErr = err as { response?: { status?: number; data?: { error?: string } } };
        setRegisterError(axiosErr.response?.data?.error || 'Registration failed');
      } else {
        setRegisterError('Registration failed');
      }
    } finally {
      setLoggingIn(false);
    }
  };

  const errorMsg = mode === 'login' ? loginError : registerError;

  return (
    <div className="h-full flex relative overflow-hidden">
      <div className="absolute inset-0 bg-gradient-to-br from-primary-950/30 via-surface-900 to-surface-950" />
      <div className="absolute top-1/4 -left-24 w-96 h-96 bg-primary-500/20 rounded-full blur-[100px]" />
      <div className="absolute bottom-1/4 -right-24 w-96 h-96 bg-violet-500/20 rounded-full blur-[100px]" />

      <div className="flex-1 flex items-center justify-center relative z-10">
        <motion.div
          initial={{ opacity: 0, y: 30 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6 }}
          className="w-full max-w-md px-8"
        >
          <div className="glass rounded-2xl p-8 border border-white/10">
            <div className="flex flex-col items-center mb-8">
              <motion.div
                initial={{ scale: 0, rotate: -180 }}
                animate={{ scale: 1, rotate: 0 }}
                transition={{ delay: 0.2, type: 'spring', stiffness: 200, damping: 20 }}
                className="w-16 h-16 rounded-2xl bg-gradient-to-br from-primary-500 to-primary-700 flex items-center justify-center mb-4 glow-primary"
              >
                <Shield className="text-white" size={32} />
              </motion.div>
              <h1 className="text-2xl font-extrabold text-gradient">Mafia City</h1>
              <p className="text-sm text-white/30 mt-1">Anti-Cheat System V6</p>
            </div>

            <form onSubmit={mode === 'login' ? handleLogin : handleRegister} className="space-y-4">
              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5 font-semibold">Username</label>
                <input
                  type="text"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-3 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 focus:ring-1 focus:ring-primary-500/20 transition-all"
                  placeholder="Enter your username"
                  autoFocus
                />
              </div>

              {mode === 'register' && (
                <motion.div initial={{ opacity: 0, height: 0 }} animate={{ opacity: 1, height: 'auto' }}>
                  <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5 font-semibold">Display Name</label>
                  <input
                    type="text"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                    className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-3 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 focus:ring-1 focus:ring-primary-500/20 transition-all"
                    placeholder="How others see you"
                  />
                </motion.div>
              )}

              <div>
                <label className="block text-[10px] text-white/40 uppercase tracking-wider mb-1.5 font-semibold">Password</label>
                <div className="relative">
                  <input
                    type={showPassword ? 'text' : 'password'}
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="w-full bg-white/5 border border-white/10 rounded-xl px-4 py-3 pr-10 text-sm text-white/80 placeholder-white/20 outline-none focus:border-primary-500/40 focus:ring-1 focus:ring-primary-500/20 transition-all"
                    placeholder="Enter your password"
                  />
                  <button type="button" onClick={() => setShowPassword(!showPassword)} className="absolute right-3 top-1/2 -translate-y-1/2 text-white/20 hover:text-white/50">
                    {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                  </button>
                </div>
              </div>

              {mode === 'login' && (
                <label className="flex items-center gap-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={rememberMe}
                    onChange={(e) => { setRememberMe(e.target.checked); localStorage.setItem('ac-remember', String(e.target.checked)); }}
                    className="w-4 h-4 rounded border-white/20 bg-white/5 accent-primary-500"
                  />
                  <span className="text-xs text-white/40">Remember Me</span>
                </label>
              )}

              {errorMsg && (
                <motion.div initial={{ opacity: 0, x: -10 }} animate={{ opacity: 1, x: 0 }}
                  className="flex items-center gap-2 text-rose-400 text-xs bg-rose-500/10 border border-rose-500/20 rounded-xl px-4 py-2.5"
                >
                  <AlertCircle size={14} /> {errorMsg}
                </motion.div>
              )}

              <motion.button
                type="submit"
                disabled={isLoggingIn}
                whileHover={{ scale: 1.01 }}
                whileTap={{ scale: 0.99 }}
                className="w-full py-3 rounded-xl bg-gradient-to-r from-primary-600 to-primary-500 text-sm font-semibold text-white shadow-lg shadow-primary-500/25 border border-primary-400/30 hover:from-primary-500 hover:to-primary-400 transition-all disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {isLoggingIn ? (
                  <svg className="animate-spin h-4 w-4" viewBox="0 0 24 24"><circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none"/><path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/></svg>
                ) : null}
                {isLoggingIn ? 'Please wait...' : mode === 'login' ? 'Sign In' : 'Create Account'}
              </motion.button>
            </form>

            <div className="mt-4 text-center">
              <button
                type="button"
                onClick={toggleMode}
                className="text-xs text-primary-400/60 hover:text-primary-400 transition-colors"
              >
                {mode === 'login' ? "Don't have an account? Create one" : 'Already have an account? Sign in'}
              </button>
            </div>


          </div>
        </motion.div>
      </div>

      <div className="hidden lg:flex flex-1 items-center justify-center relative z-10">
        <div className="space-y-6 max-w-md">
          {features.map((f, i) => (
            <motion.div
              key={f.label}
              initial={{ opacity: 0, x: 40 }}
              animate={{ opacity: 1, x: 0 }}
              transition={{ delay: 0.3 + i * 0.1 }}
              className="flex items-start gap-4 p-4 rounded-xl glass border border-white/5"
            >
              <div className="w-10 h-10 rounded-xl bg-primary-500/10 flex items-center justify-center text-primary-400 flex-shrink-0">
                <f.icon size={20} />
              </div>
              <div>
                <h3 className="text-sm font-semibold text-white/80">{f.label}</h3>
                <p className="text-xs text-white/30 mt-0.5">{f.desc}</p>
              </div>
            </motion.div>
          ))}
        </div>
      </div>
    </div>
  );
}
