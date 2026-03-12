/**
 * Types for Member management
 */

export type UserRole = 'Admin' | 'Gestor' | 'Colaborador';
export type UserStatus = 'Active' | 'Inactive';

export interface Member {
  userId: string;
  email: string;
  displayName: string;
  role: UserRole;
  status: UserStatus;
  createdAt: string;
}

export interface ListMembersResponse {
  members: Member[];
  totalCount: number;
}

export interface TeamMemberStatus {
  userId: string;
  displayName: string;
  role: UserRole;
  status: UserStatus;
  todayDurationSeconds: number;
  todayDurationFormatted: string;
  isTracking: boolean;
}

export interface TeamStatusResponse {
  members: TeamMemberStatus[];
  totalCount: number;
  activeCount: number;
  trackingCount: number;
}

export interface InviteMemberRequest {
  email: string;
  displayName: string;
  role: UserRole;
}

export interface InviteMemberResponse {
  userId: string;
  email: string;
  temporaryPassword: string;
}

export interface UpdateMemberStatusRequest {
  userId: string;
  status: 'active' | 'inactive';
}

export interface UpdateMemberRoleRequest {
  userId: string;
  role: UserRole;
}
