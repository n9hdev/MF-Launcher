import api from './api';

export interface IReportMessage {
  id: string;
  reportId: string;
  senderId: string;
  senderName: string;
  message: string;
  attachmentUrl?: string;
  createdAt: string;
}

export interface IPlayerReport {
  id: string;
  ticketType: string;
  playerName: string;
  reason: string;
  description: string;
  status: string;
  createdAt: string;
  result?: string;
  reporterId?: string;
  chatEnabled?: boolean;
  isFlagged?: boolean;
  attachmentUrl?: string;
  messages?: IReportMessage[];
  [key: string]: unknown;
}

export interface IReportSubmissionRequest {
  ticketType: string;
  playerName: string;
  reason: string;
  description: string;
}

export const reportApi = {
  getMyReports: () =>
    api.get<IPlayerReport[]>('/api/reports/my'),

  submitReport: (data: IReportSubmissionRequest) =>
    api.post<IPlayerReport>('/api/reports', data),

  uploadAttachment: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post<{ url: string }>('/api/reports/upload-attachment', fd);
  },

  getReport: (reportId: string) =>
    api.get<IPlayerReport>(`/api/reports/${reportId}`),

  getMessages: (reportId: string) =>
    api.get<{ messages: IReportMessage[] }>(`/api/reports/${reportId}/messages`),

  sendMessage: (reportId: string, message: string) =>
    api.post<{ success: boolean; message: IReportMessage }>(`/api/reports/${reportId}/messages`, { message }),

  sendAttachment: (reportId: string, file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post<{ success: boolean; message: IReportMessage }>(`/api/reports/${reportId}/messages/attachment`, fd);
  },
};
