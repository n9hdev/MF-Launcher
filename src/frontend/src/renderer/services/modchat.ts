import api from './api';

export interface IChatMessage {
  id: string;
  userId: string;
  user: string;
  message: string;
  attachmentUrl?: string;
  timeAgo: string;
  role: string;
  createdAt: string;
}

export interface IOnlineModerator {
  name: string;
  status: string;
}

export const modchatApi = {
  getMessages: () =>
    api.get<IChatMessage[]>('/api/modchat/messages'),

  getOnline: () =>
    api.get<IOnlineModerator[]>('/api/modchat/online'),

  sendMessage: (message: string) =>
    api.post<IChatMessage>('/api/modchat/send', { message }),

  sendAttachment: (file: File) => {
    const fd = new FormData();
    fd.append('file', file);
    return api.post<IChatMessage>('/api/modchat/send/attachment', fd);
  },
};
