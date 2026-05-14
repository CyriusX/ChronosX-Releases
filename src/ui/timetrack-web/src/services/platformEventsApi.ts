import { apiClient } from './apiClient';

export interface PlatformEventLogDto {
  id: string;
  eventType: string;
  severity: string;
  message: string;
  metadataJson?: string | null;
  timestampUtc: string;
}

export interface PlatformHealthDto {
  status: string;
  checksJson: string;
  lastChangedAtUtc: string;
  updatedAtUtc: string;
}

export async function listPlatformEvents(params?: {
  sinceUtc?: string;
  severity?: string;
  limit?: number;
}): Promise<PlatformEventLogDto[]> {
  const q = new URLSearchParams();
  if (params?.sinceUtc) q.set('sinceUtc', params.sinceUtc);
  if (params?.severity) q.set('severity', params.severity);
  if (params?.limit) q.set('limit', String(params.limit));

  const suffix = q.toString() ? `?${q.toString()}` : '';
  return apiClient<PlatformEventLogDto[]>(`/platform/events${suffix}`, { method: 'GET' });
}

export async function getPlatformHealth(): Promise<PlatformHealthDto> {
  return apiClient<PlatformHealthDto>('/platform/health', { method: 'GET' });
}

