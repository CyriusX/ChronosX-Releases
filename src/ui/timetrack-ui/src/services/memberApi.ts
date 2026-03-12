/**
 * Member API - HTTP client for member management
 */

import type {
  ListMembersResponse,
  TeamStatusResponse,
  InviteMemberRequest,
  InviteMemberResponse,
  UpdateMemberStatusRequest,
  UpdateMemberRoleRequest,
} from '../types/member';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

async function getAuthHeaders(accessToken: string): Promise<HeadersInit> {
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
  };
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Request failed' }));
    throw new Error(error.message || error.title || `HTTP ${response.status}`);
  }
  return response.json();
}

/**
 * List all members of the organization
 */
export async function listMembers(accessToken: string): Promise<ListMembersResponse> {
  const response = await fetch(`${API_BASE}/auth/members`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<ListMembersResponse>(response);
}

/**
 * Get team status with today's worked time
 */
export async function getTeamStatus(accessToken: string): Promise<TeamStatusResponse> {
  const response = await fetch(`${API_BASE}/auth/team/status`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<TeamStatusResponse>(response);
}

/**
 * Invite a new member to the organization
 */
export async function inviteMember(
  accessToken: string,
  request: InviteMemberRequest
): Promise<InviteMemberResponse> {
  const response = await fetch(`${API_BASE}/auth/invite`, {
    method: 'POST',
    headers: await getAuthHeaders(accessToken),
    body: JSON.stringify(request),
  });
  return handleResponse<InviteMemberResponse>(response);
}

/**
 * Update member status (activate/deactivate)
 */
export async function updateMemberStatus(
  accessToken: string,
  request: UpdateMemberStatusRequest
): Promise<void> {
  const response = await fetch(`${API_BASE}/auth/members/status`, {
    method: 'PUT',
    headers: await getAuthHeaders(accessToken),
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Request failed' }));
    throw new Error(error.message || error.title || `HTTP ${response.status}`);
  }
}

/**
 * Update member role
 */
export async function updateMemberRole(
  accessToken: string,
  request: UpdateMemberRoleRequest
): Promise<void> {
  const response = await fetch(`${API_BASE}/auth/members/role`, {
    method: 'PUT',
    headers: await getAuthHeaders(accessToken),
    body: JSON.stringify(request),
  });
  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Request failed' }));
    throw new Error(error.message || error.title || `HTTP ${response.status}`);
  }
}
