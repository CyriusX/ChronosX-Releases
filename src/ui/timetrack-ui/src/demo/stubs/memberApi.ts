import type {
  ListMembersResponse,
  TeamStatusResponse,
  MemberSummaryResponse,
} from '../../types/member';
import { demoMemberSummaryByUserId, demoTeamMembers } from '../demoData';

export async function listMembers(): Promise<ListMembersResponse> {
  const members = demoTeamMembers.map((m) => ({
    userId: m.userId,
    displayName: m.displayName,
    role: m.role as any,
    status: m.status as any,
    email: `${m.userId}@demo.local`,
    createdAt: new Date(Date.now() - 30 * 86400_000).toISOString(),
  })) as any;
  return {
    members,
    totalCount: members.length,
  };
}

export async function getTeamStatus(): Promise<TeamStatusResponse> {
  return {
    members: demoTeamMembers as any,
    totalCount: demoTeamMembers.length,
    activeCount: demoTeamMembers.filter((m) => m.status === 'Active').length,
    trackingCount: demoTeamMembers.filter((m) => m.isTracking).length,
  } as any;
}

export async function getMySummary(): Promise<MemberSummaryResponse> {
  return demoMemberSummaryByUserId['u-demo'] as any;
}

export async function getMemberSummary(userId: string): Promise<MemberSummaryResponse> {
  return (demoMemberSummaryByUserId[userId] ?? demoMemberSummaryByUserId['u-demo']) as any;
}
