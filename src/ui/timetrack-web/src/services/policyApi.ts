/**
 * Policy API — Web version using web apiClient
 */

import { api } from './apiClient';
import type {
  OrgPolicyResponse,
  UpdateOrgPolicyRequest,
  EvidencePolicyResponse,
  UpdateEvidencePolicyRequest,
} from '@desktop/types/settings';

export async function getOrgPolicy(orgId: string): Promise<OrgPolicyResponse> {
  return api.get<OrgPolicyResponse>(`/orgs/${encodeURIComponent(orgId)}/policies`);
}

export async function updateOrgPolicy(orgId: string, request: UpdateOrgPolicyRequest): Promise<OrgPolicyResponse> {
  return api.put<OrgPolicyResponse>(`/orgs/${encodeURIComponent(orgId)}/policies`, request);
}

export async function getEvidencePolicy(orgId: string): Promise<EvidencePolicyResponse> {
  return api.get<EvidencePolicyResponse>(`/orgs/${encodeURIComponent(orgId)}/policies/evidence`);
}

export async function updateEvidencePolicy(orgId: string, request: UpdateEvidencePolicyRequest): Promise<EvidencePolicyResponse> {
  return api.put<EvidencePolicyResponse>(`/orgs/${encodeURIComponent(orgId)}/policies/evidence`, request);
}
