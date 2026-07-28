import { HashRouter, Routes, Route, Navigate, useLocation } from 'react-router-dom';
import { AnimatePresence, motion } from 'framer-motion';
import { lazy, Suspense, useEffect } from 'react';
import { ThemeProvider } from './theme/ThemeProvider';
import { useAuthStore } from './stores/authStore';
import { authApi } from './services/auth';
import { connectSignalR, disconnectSignalR } from './services/signalr';
import { permissionApi } from './services/permission';
import { usePermissionStore } from './stores/permissionStore';

// Layout
import { AnimatedSidebar } from './components/layout/AnimatedSidebar';
import { FloatingTopBar } from './components/layout/FloatingTopBar';
import { InfoDrawer } from './components/layout/InfoDrawer';
import { CommandPalette } from './components/layout/CommandPalette';
import { GlobalSearch } from './components/layout/GlobalSearch';
import { ContextMenu } from './components/layout/ContextMenu';
import { ToastSystem } from './components/layout/ToastSystem';
import { FlagPlayerButton } from './components/layout/FlagPlayerButton';
import { ErrorBoundary } from './components/layout/ErrorBoundary';
import { ProtectedRoute } from './components/layout/ProtectedRoute';
import { UpdateBanner } from './components/ui/UpdateBanner';

// Lazy-loaded pages
const LoginPage = lazy(() => import('./pages/auth/LoginPage').then(m => ({ default: m.LoginPage })));
const PlayerDashboard = lazy(() => import('./pages/player/PlayerDashboard').then(m => ({ default: m.PlayerDashboard })));
const ProtectionPage = lazy(() => import('./pages/player/ProtectionPage').then(m => ({ default: m.ProtectionPage })));
const LaunchPage = lazy(() => import('./pages/player/LaunchPage').then(m => ({ default: m.LaunchPage })));
const HistoryPage = lazy(() => import('./pages/player/HistoryPage').then(m => ({ default: m.HistoryPage })));
const PlayerReportsPage = lazy(() => import('./pages/player/PlayerReportsPage').then(m => ({ default: m.PlayerReportsPage })));
const PlayerTicketDetailPage = lazy(() => import('./pages/player/PlayerTicketDetailPage').then(m => ({ default: m.PlayerTicketDetailPage })));
const ModeratorDashboard = lazy(() => import('./pages/moderator/ModeratorDashboard').then(m => ({ default: m.ModeratorDashboard })));
const ReportsQueuePage = lazy(() => import('./pages/moderator/ReportsQueuePage').then(m => ({ default: m.ReportsQueuePage })));
const ReportDetailPage = lazy(() => import('./pages/moderator/ReportDetailPage').then(m => ({ default: m.ReportDetailPage })));
const PlayerSearchPage = lazy(() => import('./pages/moderator/PlayerSearchPage').then(m => ({ default: m.PlayerSearchPage })));
const PlayerDetailPage = lazy(() => import('./pages/moderator/PlayerDetailPage').then(m => ({ default: m.PlayerDetailPage })));
const AlertsPage = lazy(() => import('./pages/moderator/AlertsPage').then(m => ({ default: m.AlertsPage })));
const ModChatPage = lazy(() => import('./pages/moderator/ModChatPage').then(m => ({ default: m.ModChatPage })));
const FlaggedPlayersPage = lazy(() => import('./pages/moderator/FlaggedPlayersPage').then(m => ({ default: m.FlaggedPlayersPage })));
const FlaggedPlayerDetailPage = lazy(() => import('./pages/moderator/FlaggedPlayerDetailPage').then(m => ({ default: m.FlaggedPlayerDetailPage })));

const AdminDashboard = lazy(() => import('./pages/admin/AdminDashboard').then(m => ({ default: m.AdminDashboard })));
const BanCenterPage = lazy(() => import('./pages/admin/BanCenterPage').then(m => ({ default: m.BanCenterPage })));
const AnalyticsPage = lazy(() => import('./pages/admin/AnalyticsPage').then(m => ({ default: m.AnalyticsPage })));
const AppealsPage = lazy(() => import('./pages/admin/AppealsPage').then(m => ({ default: m.AppealsPage })));
const WhitelistPage = lazy(() => import('./pages/admin/WhitelistPage').then(m => ({ default: m.WhitelistPage })));
const LivePlayerView = lazy(() => import('./pages/admin/LivePlayerView').then(m => ({ default: m.LivePlayerView })));
const CommandCenterPage = lazy(() => import('./pages/superadmin/CommandCenterPage').then(m => ({ default: m.CommandCenterPage })));
const TelemetryPage = lazy(() => import('./pages/superadmin/TelemetryPage').then(m => ({ default: m.TelemetryPage })));
const DetectionCenterPage = lazy(() => import('./pages/superadmin/DetectionCenterPage').then(m => ({ default: m.DetectionCenterPage })));
const RulesPage = lazy(() => import('./pages/superadmin/RulesPage').then(m => ({ default: m.RulesPage })));
const InfrastructurePage = lazy(() => import('./pages/superadmin/InfrastructurePage').then(m => ({ default: m.InfrastructurePage })));
const AuditLogPage = lazy(() => import('./pages/superadmin/AuditLogPage').then(m => ({ default: m.AuditLogPage })));
const SettingsPage = lazy(() => import('./pages/shared/SettingsPage').then(m => ({ default: m.SettingsPage })));
const NotFoundPage = lazy(() => import('./pages/shared/NotFoundPage').then(m => ({ default: m.NotFoundPage })));
const ForbiddenPage = lazy(() => import('./pages/shared/ForbiddenPage').then(m => ({ default: m.ForbiddenPage })));
const BannedPage = lazy(() => import('./pages/shared/BannedPage').then(m => ({ default: m.BannedPage })));
const AppealTicketPage = lazy(() => import('./pages/shared/AppealTicketPage').then(m => ({ default: m.AppealTicketPage })));
const ServiceDownPage = lazy(() => import('./pages/shared/ServiceDownPage').then(m => ({ default: m.ServiceDownPage })));

import { healthApi } from './services/health';
import { useUIStore } from './stores/uiStore';
import { useSessionStore } from './stores/sessionStore';
import { onBanStatus, onGameLaunchUnlocked, onPreLaunchResults, onPreLaunchStarted, requestPreLaunchScan } from './services/signalr';
import type { UserRole } from './types/global';

const roleRedirect: Record<string, string> = {
  player: '/dashboard',
  moderator: '/moderator/dashboard',
  admin: '/admin/dashboard',
  superadmin: '/superadmin/command',
};

function RequireAuth({ children, roles }: { children: React.ReactNode; roles?: UserRole[] }) {
  return <ProtectedRoute roles={roles}>{children}</ProtectedRoute>;
}

const LoadingFallback = () => (
  <div className="flex items-center justify-center h-full" style={{ color: 'var(--color-text-muted)' }}>
    Loading...
  </div>
);

function AuthenticatedApp() {
  const { user, isBanned, serviceDown, setServiceDown } = useAuthStore();
  const { sidebarCollapsed } = useUIStore();
  const { protectionActive } = useSessionStore();
  const location = useLocation();
  const defaultRoute = user ? roleRedirect[user.role] || '/dashboard' : '/dashboard';

  if (serviceDown && location.pathname !== '/service-down') {
    return <Navigate to="/service-down" replace />;
  }

  if (isBanned && location.pathname !== '/banned' && location.pathname !== '/player/appeal') {
    return <Navigate to="/banned" replace />;
  }

  useEffect(() => {
    const init = async () => {
      try {
        // Check service health before proceeding
        try {
          const { data } = await healthApi.getStatus();
          if (!data.healthy) {
            setServiceDown(true);
            return;
          }
        } catch {
          setServiceDown(true);
          return;
        }

        await connectSignalR();

        // Register SignalR event handlers now that connection is established
        onBanStatus((banInfo: unknown) => {
          const info = banInfo as { id?: string; reason?: string; type?: string; issuedBy?: string; issuedAt?: string; proofUrl?: string; durationHours?: number; bannedAt?: string };
          useAuthStore.getState().setBanned(true, {
            id: info.id || '',
            reason: info.reason || 'Your account has been banned',
            type: info.type || 'Permanent',
            issuedBy: info.issuedBy || 'System',
            issuedAt: info.issuedAt || new Date().toISOString(),
            proofUrl: info.proofUrl || undefined,
            durationHours: info.durationHours || 0,
            bannedAt: info.bannedAt || new Date().toISOString(),
          });
          useUIStore.getState().addToast({ type: 'error', title: 'You have been banned', message: info.reason || 'Your account has been banned', duration: 8000 });
        });

        onPreLaunchStarted(() => {
          useAuthStore.getState().setPreLaunchCleared(false);
          useUIStore.getState().addToast({ type: 'info', title: 'Pre-launch scan started', message: 'Checking system for threats...', duration: 2000 });
        });

        onGameLaunchUnlocked(() => {
          useAuthStore.getState().setPreLaunchCleared(true);
          useUIStore.getState().addToast({ type: 'success', title: 'Pre-launch scan clean', message: 'System ready — you can now launch the game.', duration: 4000 });
        });

        onPreLaunchResults((results: unknown) => {
          const events = results as Array<{ type?: string; severity?: string; description?: string }>;
          if (events && events.length > 0) {
            useAuthStore.getState().setPreLaunchThreats(events.length);
            useAuthStore.getState().setPreLaunchCleared(false);
            useUIStore.getState().addToast({
              type: 'warning', title: 'Threats detected during pre-launch scan',
              message: `${events.length} potential threat${events.length !== 1 ? 's' : ''} found. Game launch is blocked.`,
              duration: 8000,
            });
          }
        });

        // Don't scan if already banned — pointless and would re-detect old threats
        if (!useAuthStore.getState().isBanned) {
          requestPreLaunchScan();
        }
      } catch { /* SignalR connection will be retried automatically */ }

      // Permissions don't need SignalR
      permissionApi.getMyPermissions().then(({ data }) => {
        usePermissionStore.getState().setPermissions(data);
      }).catch(() => {});
      permissionApi.getFeatureFlags().then(({ data }) => {
        usePermissionStore.getState().setFeatureFlags(data);
      }).catch(() => {});
    };
    init();
    return () => { disconnectSignalR(); };
  }, []);

  useEffect(() => {
    if (window.electronAPI?.updateProtectionStatus) {
      window.electronAPI.updateProtectionStatus(protectionActive ? 'Active' : 'Paused');
    }
  }, [protectionActive]);

  return (
    <div className="h-screen w-screen flex flex-col" style={{ background: 'var(--color-surface-900)' }}>
      <div className="flex flex-1 overflow-hidden">
        <AnimatedSidebar />
        <div className="flex-1 flex flex-col overflow-hidden">
          <FloatingTopBar />
          <UpdateBanner />
          <main
            className="flex-1 overflow-y-auto"
            style={{
              padding: '24px',
              background: 'radial-gradient(ellipse at 50% 0%, rgba(99, 102, 241, 0.03) 0%, transparent 60%)',
            }}
          >
            <AnimatePresence mode="wait">
              <motion.div
                key={location.pathname}
                initial={{ opacity: 0, y: 8 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -8 }}
                transition={{ duration: 0.2, ease: 'easeOut' }}
              >
                <Suspense fallback={<LoadingFallback />}>
                  <Routes>
                    <Route path="/" element={<Navigate to={defaultRoute} replace />} />
                    <Route path="/forbidden" element={<ForbiddenPage />} />

                    <Route path="/service-down" element={<ServiceDownPage />} />
                    <Route path="/banned" element={<BannedPage />} />
                    <Route path="/player/appeal" element={<AppealTicketPage />} />
                    <Route path="/dashboard" element={<RequireAuth roles={['player']}><PlayerDashboard /></RequireAuth>} />
                    <Route path="/player/protection" element={<RequireAuth roles={['player']}><ProtectionPage /></RequireAuth>} />
                    <Route path="/player/launch" element={<RequireAuth roles={['player']}><LaunchPage /></RequireAuth>} />
                    <Route path="/player/history" element={<RequireAuth roles={['player']}><HistoryPage /></RequireAuth>} />
                    <Route path="/player/reports" element={<RequireAuth roles={['player']}><PlayerReportsPage /></RequireAuth>} />
                    <Route path="/player/reports/:id" element={<RequireAuth roles={['player']}><PlayerTicketDetailPage /></RequireAuth>} />

                    <Route path="/moderator/dashboard" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><ModeratorDashboard /></RequireAuth>} />
                    <Route path="/moderator/reports" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><ReportsQueuePage /></RequireAuth>} />
                    <Route path="/moderator/reports/:id" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><ReportDetailPage /></RequireAuth>} />
                    <Route path="/moderator/players" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><PlayerSearchPage /></RequireAuth>} />
                    <Route path="/moderator/players/:id" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><PlayerDetailPage /></RequireAuth>} />
                    <Route path="/moderator/alerts" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><AlertsPage /></RequireAuth>} />
                    <Route path="/moderator/chat" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><ModChatPage /></RequireAuth>} />
                    <Route path="/moderator/flagged" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><FlaggedPlayersPage /></RequireAuth>} />
                    <Route path="/moderator/flagged/:id" element={<RequireAuth roles={['moderator', 'admin', 'superadmin']}><FlaggedPlayerDetailPage /></RequireAuth>} />

                    <Route path="/admin/dashboard" element={<RequireAuth roles={['admin', 'superadmin']}><AdminDashboard /></RequireAuth>} />
                    <Route path="/admin/bans" element={<RequireAuth roles={['admin', 'superadmin']}><BanCenterPage /></RequireAuth>} />
                    <Route path="/admin/analytics" element={<RequireAuth roles={['admin', 'superadmin']}><AnalyticsPage /></RequireAuth>} />
                    <Route path="/admin/appeals" element={<RequireAuth roles={['admin', 'superadmin']}><AppealsPage /></RequireAuth>} />
                    <Route path="/admin/whitelist" element={<RequireAuth roles={['admin', 'superadmin']}><WhitelistPage /></RequireAuth>} />
                    <Route path="/admin/live-view" element={<RequireAuth roles={['admin', 'superadmin']}><LivePlayerView /></RequireAuth>} />

                    <Route path="/superadmin/command" element={<RequireAuth roles={['superadmin']}><CommandCenterPage /></RequireAuth>} />
                    <Route path="/superadmin/telemetry" element={<RequireAuth roles={['superadmin']}><TelemetryPage /></RequireAuth>} />
                    <Route path="/superadmin/detection" element={<RequireAuth roles={['superadmin']}><DetectionCenterPage /></RequireAuth>} />
                    <Route path="/superadmin/rules" element={<RequireAuth roles={['superadmin']}><RulesPage /></RequireAuth>} />
                    <Route path="/superadmin/infrastructure" element={<RequireAuth roles={['superadmin']}><InfrastructurePage /></RequireAuth>} />
                    <Route path="/superadmin/audit" element={<RequireAuth roles={['superadmin']}><AuditLogPage /></RequireAuth>} />

                    <Route path="/settings" element={<SettingsPage />} />

                    <Route path="*" element={<NotFoundPage />} />
                  </Routes>
                </Suspense>
              </motion.div>
            </AnimatePresence>
          </main>
        </div>

        <InfoDrawer />
      </div>

      <CommandPalette />
      <GlobalSearch />
      <ContextMenu />
      <ToastSystem />
      {user && (user.role === 'moderator' || user.role === 'admin' || user.role === 'superadmin') && <FlagPlayerButton />}
    </div>
  );
}

export function App() {
  const { user, token, isAuthenticated, restoringSession, refreshToken, setRestoringSession } = useAuthStore();
  const actuallyAuthd = isAuthenticated || (!!user && !!token);

  useEffect(() => {
    if (actuallyAuthd && refreshToken) {
      setRestoringSession(true);
      authApi.refresh(refreshToken).then(({ data }) => {
        useAuthStore.getState().setTokens(data.accessToken, data.refreshToken);
        const state = useAuthStore.getState();
        if (state.isBanned && !state.banInfo) {
          authApi.getActiveBan().then(({ data: banData }) => {
            if (banData.banned && banData.ban) {
              state.setBanned(true, banData.ban);
            }
          }).catch(() => {});
        }
      }).catch(() => {
        useAuthStore.getState().logout();
      }).finally(() => {
        setRestoringSession(false);
      });
    }
  }, []);

  if (restoringSession) {
    return (
      <ThemeProvider>
        <div className="h-screen w-screen flex items-center justify-center" style={{ background: 'var(--color-surface-900)' }}>
          <div className="flex flex-col items-center gap-4">
            <svg className="animate-spin h-8 w-8 text-primary-500" viewBox="0 0 24 24">
              <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" fill="none"/>
              <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z"/>
            </svg>
            <span className="text-sm text-white/40">Restoring session...</span>
          </div>
        </div>
      </ThemeProvider>
    );
  }

  return (
    <ThemeProvider>
      {!actuallyAuthd ? (
        <div className="h-screen w-screen" style={{ background: 'var(--color-surface-900)' }}>
          <HashRouter>
            <Routes>
              <Route path="*" element={<LoginPage />} />
            </Routes>
          </HashRouter>
        </div>
      ) : (
        <HashRouter>
          <ErrorBoundary>
            <AuthenticatedApp />
          </ErrorBoundary>
        </HashRouter>
      )}
    </ThemeProvider>
  );
}
