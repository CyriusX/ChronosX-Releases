import { api } from './apiClient';

export interface SmartAlertItem {
  id: string;
  alertType: string;
  message: string;
  severity: 'info' | 'warning' | 'critical';
  actionType: string | null;
  wasRead: boolean;
  wasActed: boolean;
  aboutUserId: string | null;
  createdAt: string;
}

export interface SmartAlertListResponse {
  alerts: SmartAlertItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UnreadCountResponse {
  count: number;
}

export interface TeamAlertItem {
  id: string;
  userId: string;
  aboutUserId: string | null;
  alertType: string;
  message: string;
  severity: 'info' | 'warning' | 'critical';
  actionType: string | null;
  createdAt: string;
}

export interface TeamAlertListResponse {
  alerts: TeamAlertItem[];
  total: number;
  page: number;
  pageSize: number;
}

export async function getAlerts(params?: {
  severity?: string;
  alertType?: string;
  unreadOnly?: boolean;
  page?: number;
  pageSize?: number;
}): Promise<SmartAlertListResponse> {
  const query = new URLSearchParams();
  if (params?.severity) query.set('severity', params.severity);
  if (params?.alertType) query.set('alertType', params.alertType);
  if (params?.unreadOnly) query.set('unreadOnly', 'true');
  if (params?.page) query.set('page', String(params.page));
  if (params?.pageSize) query.set('pageSize', String(params.pageSize));
  const qs = query.toString();
  return api.get<SmartAlertListResponse>(`/alerts${qs ? `?${qs}` : ''}`);
}

export async function getUnreadAlertCount(): Promise<UnreadCountResponse> {
  return api.get<UnreadCountResponse>('/alerts/unread-count');
}

export async function markAlertRead(id: string): Promise<void> {
  await api.post(`/alerts/${id}/read`);
}

export async function markAllAlertsRead(): Promise<void> {
  await api.post('/alerts/read-all');
}

export async function markAlertActed(id: string): Promise<void> {
  await api.post(`/alerts/${id}/act`);
}

export async function dismissAlert(id: string): Promise<void> {
  await api.delete(`/alerts/${id}`);
}

export async function getTeamAlerts(params?: {
  severity?: string;
  alertType?: string;
  aboutUserId?: string;
  page?: number;
  pageSize?: number;
}): Promise<TeamAlertListResponse> {
  const query = new URLSearchParams();
  if (params?.severity) query.set('severity', params.severity);
  if (params?.alertType) query.set('alertType', params.alertType);
  if (params?.aboutUserId) query.set('aboutUserId', params.aboutUserId);
  if (params?.page) query.set('page', String(params.page));
  if (params?.pageSize) query.set('pageSize', String(params.pageSize));
  const qs = query.toString();
  return api.get<TeamAlertListResponse>(`/alerts/team${qs ? `?${qs}` : ''}`);
}
