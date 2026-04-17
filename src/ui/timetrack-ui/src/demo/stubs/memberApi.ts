import type {
  ListMembersResponse,
  TeamStatusResponse,
  MemberSummaryResponse,
} from '../../types/member';
import { demoMemberSummaryByUserId, demoTeamMembers } from '../demoData';

export async function listMembers(): Promise<ListMembersResponse> {
  return {
    members: demoTeamMembers.map((m) => ({
      userId: m.userId,
      displayName: m.displayName,
      role: m.role as any,
      status: m.status as any,
      email: `${m.userId}@demo.local`,
    })) as any,
  };
}

export async function getTeamStatus(): Promise<TeamStatusResponse> {
  return {
    members: demoTeamMembers as any,
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

