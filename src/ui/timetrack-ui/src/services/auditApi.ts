import { api } from './apiClient';

export interface EvidenceAccessLogItem {
  id: string;
  actorUserId: string | null;
  actorName: string | null;
  actorEmail: string | null;
  targetUserId: string | null;
  evidenceId: string | null;
  action: string;
  accessedAt: string;
  ipAddress: string | null;
}

export interface EvidenceAccessLogResponse {
  items: EvidenceAccessLogItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export async function getEvidenceAccessLogs(
  orgId: string,
  params: {
    page?: number;
    pageSize?: number;
    startDate?: string;
    endDate?: string;
    actorUserId?: string;
    targetUserId?: string;
    action?: string;
  } = {}
): Promise<EvidenceAccessLogResponse> {
  const searchParams = new URLSearchParams();
  if (params.page) searchParams.set('page', params.page.toString());
  if (params.pageSize) searchParams.set('pageSize', params.pageSize.toString());
  if (params.startDate) searchParams.set('startDate', params.startDate);
  if (params.endDate) searchParams.set('endDate', params.endDate);
  if (params.actorUserId) searchParams.set('actorUserId', params.actorUserId);
  if (params.targetUserId) searchParams.set('targetUserId', params.targetUserId);
  if (params.action) searchParams.set('action', params.action);

  return api.get<EvidenceAccessLogResponse>(
    `/orgs/${orgId}/audit/evidence-access?${searchParams.toString()}`
  );
}
