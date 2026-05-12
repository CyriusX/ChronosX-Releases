import { api } from './apiClient';

export interface StorageUsageResponse {
  usedBytes: number;
  quotaBytes: number;
  percentage: number;
  quotaGb: number;
  usedGb: number;
}

export async function getStorageUsage(orgId: string): Promise<StorageUsageResponse> {
  return api.get<StorageUsageResponse>(`/orgs/${orgId}/storage/usage`);
}

export async function updateStorageQuota(orgId: string, quotaGb: number): Promise<StorageUsageResponse> {
  return api.put<StorageUsageResponse>(`/orgs/${orgId}/storage/quota`, { quotaGb });
}
