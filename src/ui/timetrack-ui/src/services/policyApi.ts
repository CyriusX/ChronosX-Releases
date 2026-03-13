/**
 * Policy API - HTTP client for organization policies
 */

import type {
  OrgPolicyResponse,
  UpdateOrgPolicyRequest,
} from '../types/settings';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

async function getAuthHeaders(accessToken: string): Promise<HeadersInit> {
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
  };
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let errorMessage = `HTTP ${response.status}`;

    try {
      const errorData = await response.json();
      // Extract error message from various formats
      errorMessage = errorData.message
        || errorData.title
        || errorData.error
        || (typeof errorData === 'string' ? errorData : null)
        || errorMessage;
    } catch {
      // Failed to parse error response
    }

    throw new Error(errorMessage);
  }
  return response.json();
}

/**
 * Get organization policy
 * GET /api/v1/orgs/{orgId}/policies
 */
export async function getOrgPolicy(
  accessToken: string,
  orgId: string
): Promise<OrgPolicyResponse> {
  const response = await fetch(`${API_BASE}/orgs/${orgId}/policies`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<OrgPolicyResponse>(response);
}

/**
 * Update organization policy (Admin only)
 * PUT /api/v1/orgs/{orgId}/policies
 */
export async function updateOrgPolicy(
  accessToken: string,
  orgId: string,
  request: UpdateOrgPolicyRequest
): Promise<OrgPolicyResponse> {
  const response = await fetch(`${API_BASE}/orgs/${orgId}/policies`, {
    method: 'PUT',
    headers: await getAuthHeaders(accessToken),
    body: JSON.stringify(request),
  });
  return handleResponse<OrgPolicyResponse>(response);
}
