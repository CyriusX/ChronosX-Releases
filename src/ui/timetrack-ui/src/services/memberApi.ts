/**
 * Member API - HTTP client for member management
 *
 * Uses centralized apiClient for automatic 401 handling
 */

import { api } from './apiClient';
import { getUserTimezone } from '../types/reports';
import type {
  ListMembersResponse,
  TeamStatusResponse,
  MemberSummaryResponse,
  InviteMemberRequest,
  InviteMemberResponse,
  UpdateMemberStatusRequest,
  UpdateMemberRoleRequest,
} from '../types/member';

/**
 * List all members of the organization
 */
export async function listMembers(): Promise<ListMembersResponse> {
  return api.get<ListMembersResponse>('/auth/members');
}

/**
 * Get team status with today's worked time
 */
export async function getTeamStatus(): Promise<TeamStatusResponse> {
  const params = new URLSearchParams({ timezone: getUserTimezone() });
  return api.get<TeamStatusResponse>(`/auth/team/status?${params.toString()}`);
}

/**
 * Get a specific member's today summary (admin/manager view)
 */
export async function getMemberSummary(userId: string): Promise<MemberSummaryResponse> {
  const params = new URLSearchParams({ timezone: getUserTimezone() });
  return api.get<MemberSummaryResponse>(`/auth/team/members/${encodeURIComponent(userId)}/summary?${params.toString()}`);
}

/**
 * Invite a new member to the organization
 */
export async function inviteMember(request: InviteMemberRequest): Promise<InviteMemberResponse> {
  return api.post<InviteMemberResponse>('/auth/invite', request);
}

/**
 * Update member status (activate/deactivate)
 */
export async function updateMemberStatus(request: UpdateMemberStatusRequest): Promise<void> {
  return api.put<void>('/auth/members/status', request);
}

/**
 * Update member role
 */
export async function updateMemberRole(request: UpdateMemberRoleRequest): Promise<void> {
  return api.put<void>('/auth/members/role', request);
}
