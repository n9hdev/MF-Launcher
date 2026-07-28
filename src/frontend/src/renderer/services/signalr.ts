import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useAuthStore } from '../stores/authStore';
import { resolveApiBaseUrl } from './apiConfig';

let connection: HubConnection | null = null;
let reconnectAttempts = 0;
const MAX_RECONNECT_ATTEMPTS = 10;
const RECONNECT_BASE_DELAY = 1000;

export function getSignalRConnection(): HubConnection | null {
  return connection;
}

export async function connectSignalR(): Promise<HubConnection> {
  const token = useAuthStore.getState().token;
  const user = useAuthStore.getState().user;
  if (!token) throw new Error('No auth token available');

  const baseUrl = resolveApiBaseUrl();

  connection = new HubConnectionBuilder()
    .withUrl(`${baseUrl}/hub/anticheat`, {
      accessTokenFactory: () => useAuthStore.getState().token || '',
    })
    .withAutomaticReconnect([0, 1000, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();

  connection.onreconnecting(() => {
    reconnectAttempts++;
    if (reconnectAttempts > MAX_RECONNECT_ATTEMPTS) {
      connection?.stop();
      return;
    }
  });

  connection.onreconnected(() => {
    reconnectAttempts = 0;
    const role = useAuthStore.getState().user?.role;
    if (role) {
      connection?.invoke('JoinRoleGroup', role).catch(() => {});
    }
  });

  connection.onclose(() => {
    connection = null;
  });

  await connection.start();
  reconnectAttempts = 0;

  if (user?.role) {
    await connection.invoke('JoinRoleGroup', user.role);
  }

  if (user?.id) {
    await connection.invoke('JoinUserGroup', user.id);
  }

  return connection;
}

export async function disconnectSignalR(): Promise<void> {
  if (connection) {
    await connection.stop();
    connection = null;
  }
}

export function onDetectionEvent(callback: (event: unknown) => void): void {
  connection?.off('DetectionEvent');
  connection?.on('DetectionEvent', callback);
}

export function onStatusUpdate(callback: (status: unknown) => void): void {
  connection?.off('StatusUpdate');
  connection?.on('StatusUpdate', callback);
}

export function onScanResults(callback: (results: unknown) => void): void {
  connection?.off('ScanResults');
  connection?.on('ScanResults', callback);
}

export function onBanStatus(callback: (banInfo: unknown) => void): void {
  connection?.off('BanStatus');
  connection?.on('BanStatus', callback);
}

export function onTrustStatusChanged(callback: (trustStatus: unknown) => void): void {
  connection?.off('TrustStatusChanged');
  connection?.on('TrustStatusChanged', callback);
}

export function onHwidVerified(callback: (data: { verified: boolean }) => void): void {
  connection?.off('HwidVerified');
  connection?.on('HwidVerified', callback);
}

export async function requestPreLaunchScan(): Promise<void> {
  if (connection?.state === 'Connected') {
    await connection.invoke('RequestPreLaunchScan');
  }
}

export function onPreLaunchStarted(callback: () => void): void {
  connection?.off('PreLaunchStarted');
  connection?.on('PreLaunchStarted', callback);
}

export function onPreLaunchResults(callback: (results: unknown) => void): void {
  connection?.off('PreLaunchResults');
  connection?.on('PreLaunchResults', callback);
}

export function onGameLaunchUnlocked(callback: () => void): void {
  connection?.off('GameLaunchUnlocked');
  connection?.on('GameLaunchUnlocked', callback);
}

export async function joinUserGroup(playerId: string): Promise<void> {
  if (connection?.state === 'Connected') {
    await connection.invoke('JoinUserGroup', playerId);
  }
}

export async function leaveUserGroup(playerId: string): Promise<void> {
  if (connection?.state === 'Connected') {
    await connection.invoke('LeaveUserGroup', playerId);
  }
}
