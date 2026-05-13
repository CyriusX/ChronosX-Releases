/**
 * Policy API - HTTP client for organization policies
 *
 * Uses centralized apiClient for automatic 401 handling
 */

import { api } from './apiClient';
import type {
  OrgPolicyResponse,
  UpdateOrgPolicyRequest,
  EvidencePolicyResponse,
  UpdateEvidencePolicyRequest,
} from '../types/settings';

/**
 * Get organization policy
 * GET /api/v1/orgs/{orgId}/policies
 */
export async function getOrgPolicy(orgId: string): Promise<OrgPolicyResponse> {
  return api.get<OrgPolicyResponse>(`/orgs/${orgId}/policies`);
}

/**
 * Update organization policy (Admin only)
 * PUT /api/v1/orgs/{orgId}/policies
 */
export async function updateOrgPolicy(
  orgId: string,
  request: UpdateOrgPolicyRequest
): Promise<OrgPolicyResponse> {
  return api.put<OrgPolicyResponse>(`/orgs/${orgId}/policies`, request);
}

/**
 * Get evidence policy
 * GET /api/v1/orgs/{orgId}/policies/evidence
 */
export async function getEvidencePolicy(orgId: string): Promise<EvidencePolicyResponse> {
  return api.get<EvidencePolicyResponse>(`/orgs/${orgId}/policies/evidence`);
}

/**
 * Update evidence policy (Admin/Manager only)
 * PUT /api/v1/orgs/{orgId}/policies/evidence
 */
export async function updateEvidencePolicy(
  orgId: string,
  request: UpdateEvidencePolicyRequest
): Promise<EvidencePolicyResponse> {
  return api.put<EvidencePolicyResponse>(`/orgs/${orgId}/policies/evidence`, request);
}
