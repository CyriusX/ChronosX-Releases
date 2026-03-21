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

/**
 * Response from GET /api/v1/auth/team/members/:userId/summary
 * Same shape as TodaySummaryResponse so it can be used interchangeably on the dashboard
 */
export interface MemberSummaryResponse {
  totalDuration: number;
  productiveTime: number;
  idleTime: number;
  focusTime: number;
  focusScore: number;
  sessionsCount: number;
  topProjects: { name: string; duration: number; percentage: number }[];
  topApplications: { name: string; duration: number; percentage: number; iconPath?: string; productivity?: 'productive' | 'neutral' | 'distraction'; subcategory?: string; source?: 'global' | 'org_override' | 'default' }[];
  topAppsByExe?: { name: string; duration: number; percentage: number; iconPath?: string; productivity?: 'productive' | 'neutral' | 'distraction'; subcategory?: string; source?: 'global' | 'org_override' | 'default' }[];
  categories: { name: string; duration: number; percentage: number; color: string; subcategory?: string; productivity?: 'productive' | 'neutral' | 'distraction'; source?: 'global' | 'org_override' | 'default' }[];
  weeklyHistory: { date: string; dayName: string; hours: number; isToday: boolean }[];
  lastSyncAt?: string | null;
}
