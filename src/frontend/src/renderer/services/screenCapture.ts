import api from './api';
import { HubConnectionBuilder, HubConnection, LogLevel } from '@microsoft/signalr';
import { useAuthStore } from '../stores/authStore';
import { resolveApiBaseUrl } from './apiConfig';

export interface IScreenshotRequest {
  playerId: string;
  detectionEventId?: string;
  reason?: string;
}

export interface IScreenshotCapture {
  id: string;
  playerId: string;
  detectionEventId?: string;
  sessionId?: string;
  imageData?: string;
  format: string;
  riskScore: number;
  capturedAt: string;
  capturedBy?: string;
  hmacSignature?: string;
  storagePath?: string;
}

export interface IStreamSession {
  sessionId: string;
  playerId: string;
  status: string;
  startedAt: string;
  endedAt?: string;
  totalFrames: number;
  durationSeconds: number;
  linkedDetectionId?: string;
  viewers: IStreamViewer[];
  targetFps: number;
  jpegQuality: number;
}

export interface IStreamViewer {
  adminId: string;
  adminName: string;
  connectionId: string;
  joinedAt: string;
  leftAt?: string;
}

export interface IScreenFrame {
  sessionId: string;
  frameNumber: number;
  imageData: string;
  width: number;
  height: number;
  format: string;
  timestamp: string;
}

export interface IStreamSummary {
  sessionId: string;
  playerId: string;
  status: string;
  startedAt: string;
  durationSeconds: number;
  totalFrames: number;
  viewerCount: number;
  linkedDetectionId?: string;
}

export interface IUploadProofResponse {
  url: string;
  fileName: string;
  playerId?: string;
  detectionEventId?: string;
  reason?: string;
}

export const screenCaptureApi = {
  captureScreenshot: (req: IScreenshotRequest) =>
    api.post<IScreenshotCapture>('/api/screen/capture', req),

  requestScreenshotFromPlayer: (playerId: string, reason?: string) =>
    api.post<{ requestId: string; playerId: string; hardwareId: string }>(
      `/api/service/request-screenshot/${playerId}`,
      null,
      { params: { reason } }
    ),

  startStreamOnPlayer: (playerId: string, detectionEventId?: string) =>
    api.post<{ sessionId: string; playerId: string; targetFps: number; jpegQuality: number }>(
      `/api/service/start-stream/${playerId}`,
      null,
      { params: { detectionEventId } }
    ),

  stopStreamOnPlayer: (playerId: string) =>
    api.post<{ playerId: string }>(`/api/service/stop-stream/${playerId}`),

  getScreenshotHistory: (playerId: string, limit = 50) =>
    api.get<IScreenshotCapture[]>(`/api/screen/capture/${playerId}?limit=${limit}`),

  getScreenshot: (id: string) =>
    api.get<IScreenshotCapture>(`/api/screen/capture/detail/${id}`),

  signScreenshot: (id: string, secret: string) =>
    api.post<{ id: string; signature: string }>(`/api/screen/capture/${id}/sign?secret=${secret}`),

  createStream: (playerId: string, detectionEventId?: string) =>
    api.post<IStreamSession>('/api/screen/stream/create', { playerId, detectionEventId }),

  getActiveStreams: () =>
    api.get<IStreamSummary[]>('/api/screen/stream/active'),

  getStreamHistory: (playerId: string, limit = 20) =>
    api.get<IStreamSummary[]>(`/api/screen/stream/${playerId}?limit=${limit}`),

  endStream: (sessionId: string) =>
    api.post<{ message: string }>(`/api/screen/stream/${sessionId}/end`),

  updateFps: (sessionId: string, fps: number) =>
    api.post<{ sessionId: string; targetFps: number }>(`/api/screen/stream/${sessionId}/fps?fps=${fps}`),

  linkEvidence: (sessionId: string, eventId: string, processId: number) =>
    api.post(`/api/screen/stream/${sessionId}/link-evidence`, { eventId, processId }),

  uploadProof: (file: File, playerId?: string, detectionEventId?: string, reason?: string) => {
    const formData = new FormData();
    formData.append('file', file);
    if (playerId) formData.append('playerId', playerId);
    if (detectionEventId) formData.append('detectionEventId', detectionEventId);
    if (reason) formData.append('reason', reason);
    return api.post<IUploadProofResponse>('/api/screen/upload-proof', formData, {
      timeout: 60000,
      params: { playerId, detectionEventId, reason },
    });
  },
};

let streamConnection: HubConnection | null = null;

export function getScreenStreamConnection(): HubConnection | null {
  return streamConnection;
}

export async function connectScreenStream(): Promise<HubConnection> {
  const token = useAuthStore.getState().token;
  if (!token) throw new Error('No auth token');

  const baseUrl = resolveApiBaseUrl();

  streamConnection = new HubConnectionBuilder()
    .withUrl(`${baseUrl}/hub/screenstream`, { accessTokenFactory: () => token })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(LogLevel.Warning)
    .build();

  await streamConnection.start();
  return streamConnection;
}

export async function disconnectScreenStream(): Promise<void> {
  if (streamConnection) {
    await streamConnection.stop();
    streamConnection = null;
  }
}

export function onFrameReceived(callback: (frame: IScreenFrame) => void): void {
  streamConnection?.on('FrameReceived', callback);
}

export function offFrameReceived(callback: (frame: IScreenFrame) => void): void {
  streamConnection?.off('FrameReceived', callback);
}

export function onStreamEnded(callback: (data: { sessionId: string }) => void): void {
  streamConnection?.on('StreamEnded', callback);
}

export function offStreamEnded(callback: (data: { sessionId: string }) => void): void {
  streamConnection?.off('StreamEnded', callback);
}

export function onStreamError(callback: (message: string) => void): void {
  streamConnection?.on('StreamError', callback);
}

export function offStreamError(callback: (message: string) => void): void {
  streamConnection?.off('StreamError', callback);
}
