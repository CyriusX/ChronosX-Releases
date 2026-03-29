/**
 * Member API — Web version using web apiClient
 */

import { api } from './apiClient';
import { getUserTimezone } from '@desktop/types/reports';
import type {
  ListMembersResponse,
  TeamStatusResponse,
  MemberSummaryResponse,
  InviteMemberRequest,
  InviteMemberResponse,
  UpdateMemberStatusRequest,
  UpdateMemberRoleRequest,
} from '@desktop/types/member';

export type {
  ListMembersResponse,
  TeamStatusResponse,
  MemberSummaryResponse,
  InviteMemberRequest,
  InviteMemberResponse,
  UpdateMemberStatusRequest,
  UpdateMemberRoleRequest,
};

export async function listMembers(): Promise<ListMembersResponse> {
  return api.get<ListMembersResponse>('/auth/members');
}

export async function getTeamStatus(): Promise<TeamStatusResponse> {
  const params = new URLSearchParams({ timezone: getUserTimezone() });
  return api.get<TeamStatusResponse>(`/auth/team/status?${params.toString()}`);
}

export async function getMemberSummary(userId: string): Promise<MemberSummaryResponse> {
  const params = new URLSearchParams({ timezone: getUserTimezone() });
  return api.get<MemberSummaryResponse>(`/auth/team/members/${encodeURIComponent(userId)}/summary?${params.toString()}`);
}

export async function inviteMember(request: InviteMemberRequest): Promise<InviteMemberResponse> {
  return api.post<InviteMemberResponse>('/auth/invite', request);
}

export async function updateMemberStatus(request: UpdateMemberStatusRequest): Promise<void> {
  return api.put<void>('/auth/members/status', request);
}

export async function updateMemberRole(request: UpdateMemberRoleRequest): Promise<void> {
  return api.put<void>('/auth/members/role', request);
}
