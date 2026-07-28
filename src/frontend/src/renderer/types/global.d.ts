export type UserRole = 'player' | 'moderator' | 'admin' | 'superadmin';

export type Severity = 'low' | 'medium' | 'high' | 'critical';

export type ModuleStatus = 'active' | 'inactive' | 'error' | 'warning';

export type TrustStatus = 'trusted' | 'pending' | 'restricted';

export interface IUser {
  id: string;
  username: string;
  displayName: string;
  role: UserRole;
  avatar?: string;
  banner?: string;
  email?: string;
  trustScore: number;
  trustStatus: TrustStatus;
  level: number;
  xp: number;
  nextLevelXp: number;
  status: 'online' | 'idle' | 'dnd' | 'offline';
  createdAt: string;
  lastLogin: string;
  badges: IBadge[];
}

export interface IBadge {
  id: string;
  name: string;
  icon: string;
  color: string;
}

export interface IDetectionEvent {
  id: string;
  type: string;
  severity: Severity;
  timestamp: string;
  description: string;
  confidence: number;
  evidencePath?: string;
  playerId?: string;
  playerName?: string;
  processName?: string;
  moduleName?: string;
  ruleName?: string;
  resolved: boolean;
  resolvedAt?: string;
  resolvedBy?: string;
}

export interface IProtectionStatus {
  memoryScanner: ModuleStatus;
  processAnalyzer: ModuleStatus;
  injectionDetector: ModuleStatus;
  kernelScanner: ModuleStatus;
  yaraScanner: ModuleStatus;
  networkMonitor: ModuleStatus;
  fileIntegrity: ModuleStatus;
}

export interface ISystemHealth {
  cpuUsage: number;
  memoryUsage: number;
  networkLatency: number;
  uptime: string;
  lastScanTime: string;
  activeModules: number;
  totalModules: number;
  fps: number;
  processesMonitored: number;
}

export interface IMetric {
  label: string;
  value: string | number;
  subtitle?: string;
  trend?: 'up' | 'down' | 'neutral';
  trendValue?: string;
  icon?: string;
}

export interface INotification {
  id: string;
  type: 'info' | 'warning' | 'error' | 'success' | 'achievement';
  title: string;
  message: string;
  timestamp: string;
  read: boolean;
  actionUrl?: string;
  image?: string;
}

export interface ICommand {
  id: string;
  label: string;
  description: string;
  category: string;
  shortcut?: string;
  icon?: string;
  action: () => void;
}

export interface IPlayer {
  id: string;
  username: string;
  email?: string;
  trustScore: number;
  status: 'online' | 'offline' | 'suspected';
  lastSeen: string;
  gameName: string;
  hoursPlayed: number;
  reportsCount: number;
  bansCount: number;
  avatar?: string;
}

export interface IReport {
  id: string;
  playerId: string;
  playerName: string;
  reporterName: string;
  reason: string;
  description: string;
  evidence: string[];
  status: 'pending' | 'investigating' | 'resolved' | 'dismissed';
  severity: Severity;
  createdAt: string;
  assignedTo?: string;
  resolvedAt?: string;
}

export interface IBanEntry {
  id: string;
  playerId: string;
  playerName: string;
  reason: string;
  type: 'permanent' | 'temporary';
  duration?: string;
  issuedBy: string;
  issuedAt: string;
  expiresAt?: string;
  active: boolean;
  appealable: boolean;
  evidenceRef?: string;
}

export interface IAppeal {
  id: string;
  playerId: string;
  playerName: string;
  banId: string;
  reason: string;
  message: string;
  status: 'pending' | 'approved' | 'denied';
  createdAt: string;
  reviewedBy?: string;
  reviewedAt?: string;
}

export interface IPermission {
  id: string;
  name: string;
  description: string;
  granted: boolean;
}

export interface IRolePermissions {
  role: UserRole;
  permissions: IPermission[];
}

export interface IBanInfo {
  id: string;
  reason: string;
  type: string;
  issuedBy: string;
  issuedAt: string;
  proofUrl?: string;
  durationHours: number;
  bannedAt: string;
}

export interface IToast {
  id: string;
  type: 'success' | 'error' | 'warning' | 'info';
  title: string;
  message?: string;
  duration?: number;
}
