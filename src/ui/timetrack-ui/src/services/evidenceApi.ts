import { api } from './apiClient';
import type { PagedEvidenceResponse, PresignedDownloadUrlResponse, BatchDownloadUrlItem } from '../types/evidence';

export async function getEvidenceByPeriod(
  startDate: string,
  endDate: string,
  page: number = 1,
  pageSize: number = 50,
  userId?: string
): Promise<PagedEvidenceResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    page: page.toString(),
    pageSize: pageSize.toString(),
  });
  if (userId) {
    params.append('userId', userId);
  }
  return api.get<PagedEvidenceResponse>(`/evidence?${params.toString()}`);
}

export async function getEvidenceDownloadUrl(
  evidenceId: string
): Promise<PresignedDownloadUrlResponse> {
  return api.get<PresignedDownloadUrlResponse>(`/evidence/${evidenceId}/download-url`);
}

export async function getBatchDownloadUrls(
  evidenceIds: string[]
): Promise<BatchDownloadUrlItem[]> {
  return api.post<BatchDownloadUrlItem[]>('/evidence/batch-download-urls', evidenceIds);
}
