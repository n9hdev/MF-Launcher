import api from './api';

export interface ISuperAdminStats {
  totalUsers: number;
  activeSessions: number;
  detectionEngineUptime: number;
  systemLoad: number;
  dataProcessed: string;
}

export interface IInfrastructureNode {
  name: string;
  status: string;
  uptime: string;
  load: string;
  region: string;
}

export interface ISystemHealth {
  cpu: number;
  memory: number;
  disk: number;
  network: number;
}

export interface ITelemetryMetric {
  label: string;
  value: string;
  change: string;
  trend: string;
}

export interface ISystemResource {
  label: string;
  value: number;
  color: string;
}

export interface IDetectionCenterStats {
  detectionRate: number;
  engineVersion: string;
  uptime: string;
  configVersion: number;
}

export interface IModuleStatus {
  name: string;
  status: string;
}

export interface IEngineConfig {
  label: string;
  value: string;
}

export interface IRuleConditions {
  minApiCount?: number;
  apis?: string[];
  keywords?: string[];
  entropyThreshold?: number;
  codeEntropyThreshold?: number;
  luaDlls?: string[];
  gameFilePrefixes?: string[];
  suspiciousSectionNames?: string[];
}

export interface IRuleEntry {
  id: string;
  name: string;
  description: string;
  severity: string;
  category: string;
  matchType: string;
  conditions: IRuleConditions | null;
  patterns: string[];
  tags: string[];
  enabled: boolean;
  hitCount: number;
  lastMatchTime: string | null;
  createdAt: string;
  updatedAt: string;
  [key: string]: unknown;
}

export interface IServerNode {
  name: string;
  type: string;
  status: string;
  cpu: number;
  mem: number;
  disk: number;
  region: string;
}

export interface IInfrastructureStats {
  totalServers: number;
  online: number;
  avgCpu: number;
  avgMem: number;
}

export interface IAuditLogEntry {
  id: string;
  action: string;
  user: string;
  target: string;
  details: string;
  timestamp: string;
  ip: string;
  [key: string]: unknown;
}

export const superAdminApi = {
  getStats: () =>
    api.get<ISuperAdminStats>('/api/superadmin/stats'),

  getInfrastructureNodes: () =>
    api.get<IInfrastructureNode[]>('/api/superadmin/infrastructure/nodes'),

  getSystemHealth: () =>
    api.get<ISystemHealth>('/api/superadmin/infrastructure/health'),

  getTelemetryMetrics: () =>
    api.get<ITelemetryMetric[]>('/api/superadmin/telemetry/metrics'),

  getSystemResources: () =>
    api.get<ISystemResource[]>('/api/superadmin/telemetry/resources'),

  getDetectionCenterStats: () =>
    api.get<IDetectionCenterStats>('/api/superadmin/detection/stats'),

  getModuleStatuses: () =>
    api.get<IModuleStatus[]>('/api/superadmin/detection/modules'),

  getEngineConfig: () =>
    api.get<IEngineConfig[]>('/api/superadmin/detection/config'),

  getRules: () =>
    api.get<IRuleEntry[]>('/api/rules'),
  createRule: (data: Partial<IRuleEntry>) =>
    api.post<IRuleEntry>('/api/rules', data),
  updateRule: (id: string, data: Partial<IRuleEntry>) =>
    api.put<IRuleEntry>(`/api/rules/${id}`, data),
  deleteRule: (id: string) =>
    api.delete(`/api/rules/${id}`),
  toggleRule: (id: string) =>
    api.patch<IRuleEntry>(`/api/rules/${id}/toggle`),

  getServers: () =>
    api.get<IServerNode[]>('/api/superadmin/infrastructure/servers'),

  getInfrastructureStats: () =>
    api.get<IInfrastructureStats>('/api/superadmin/infrastructure/server-stats'),

  getAuditLogs: () =>
    api.get<IAuditLogEntry[]>('/api/superadmin/audit-logs'),
};
