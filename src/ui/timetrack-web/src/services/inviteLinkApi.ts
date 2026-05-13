import { api } from './apiClient';

export interface GenerateInviteLinkResponse {
  id: string;
  token: string;
  linkUrl: string;
}

export interface InviteLinkItem {
  id: string;
  role: string;
  useCount: number;
  expiresAt: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface ListInviteLinksResponse {
  links: InviteLinkItem[];
}

export interface InviteLinkInfoResponse {
  orgName: string;
  role: string;
  isValid: boolean;
}

export interface RegisterViaInviteLinkRequest {
  displayName: string;
  email: string;
  password: string;
}

const getApiBaseUrl = (): string => {
  let url: string | undefined;
  if (typeof window !== 'undefined' && window.__APP_CONFIG__?.VITE_API_URL) {
    url = window.__APP_CONFIG__.VITE_API_URL;
  }
  if (!url) {
    url = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';
  }
  return url.replace(/\/+$/, '');
};

const API_BASE = getApiBaseUrl();

export const inviteLinkApi = {
  generate: () =>
    api.post<GenerateInviteLinkResponse>('/invite-links'),

  list: () =>
    api.get<ListInviteLinksResponse>('/invite-links'),

  revoke: (linkId: string) =>
    api.delete<void>(`/invite-links/${linkId}`),

  getInfo: async (token: string): Promise<InviteLinkInfoResponse> => {
    const response = await fetch(`${API_BASE}/invite-links/${token}/info`);
    if (!response.ok) {
      throw new Error('Failed to fetch invite link info');
    }
    return response.json();
  },

  registerViaLink: async (token: string, body: RegisterViaInviteLinkRequest) => {
    const response = await fetch(`${API_BASE}/invite-links/${token}/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Registration failed' }));
      throw new Error(error.message || error.title || 'Registration failed');
    }
    return response.json();
  },

  forgotPassword: async (email: string): Promise<void> => {
    await fetch(`${API_BASE}/auth/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email }),
    });
  },

  resetPassword: async (token: string, newPassword: string): Promise<void> => {
    const response = await fetch(`${API_BASE}/auth/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, newPassword }),
    });
    if (!response.ok) {
      const error = await response.json().catch(() => ({ message: 'Reset failed' }));
      throw new Error(error.message || error.title || 'Password reset failed');
    }
  },
};
