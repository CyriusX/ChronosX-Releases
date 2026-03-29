/**
 * Policy API — Web version using web apiClient
 */

import { api } from './apiClient';
import type { OrgPolicyResponse, UpdateOrgPolicyRequest } from '@desktop/types/settings';

export async function getOrgPolicy(orgId: string): Promise<OrgPolicyResponse> {
  return api.get<OrgPolicyResponse>(`/orgs/${encodeURIComponent(orgId)}/policies`);
}

export async function updateOrgPolicy(orgId: string, request: UpdateOrgPolicyRequest): Promise<OrgPolicyResponse> {
  return api.put<OrgPolicyResponse>(`/orgs/${encodeURIComponent(orgId)}/policies`, request);
}
