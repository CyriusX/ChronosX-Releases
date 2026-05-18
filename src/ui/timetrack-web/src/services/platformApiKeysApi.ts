import { apiClient } from './apiClient';

export interface PlatformApiKeyDto {
  id: string;
  label: string;
  createdAtUtc: string;
  revokedAtUtc?: string | null;
  lastUsedAtUtc?: string | null;
}

export interface CreatePlatformApiKeyResponse extends PlatformApiKeyDto {
  token: string;
}

export async function listPlatformApiKeys(): Promise<PlatformApiKeyDto[]> {
  return apiClient<PlatformApiKeyDto[]>('/platform/api-keys', { method: 'GET' });
}

export async function createPlatformApiKey(label: string): Promise<CreatePlatformApiKeyResponse> {
  return apiClient<CreatePlatformApiKeyResponse>('/platform/api-keys', {
    method: 'POST',
    body: { label },
  });
}

export async function revokePlatformApiKey(apiKeyId: string): Promise<PlatformApiKeyDto> {
  return apiClient<PlatformApiKeyDto>(`/platform/api-keys/${apiKeyId}`, { method: 'DELETE' });
}

