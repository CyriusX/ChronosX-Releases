import { useState, useCallback } from 'react';
import { useAuthStore } from '../stores/authStore';
import { getTeamStatus, listMembers } from '../services/memberApi';
import type { TeamMemberStatus, Member } from '../types/member';

interface UseTeamStatusReturn {
  members: TeamMemberStatus[];
  isLoading: boolean;
  error: string | null;
  activeCount: number;
  trackingCount: number;
  loadTeamStatus: () => Promise<void>;
}

export function useTeamStatus(): UseTeamStatusReturn {
  const { tokens } = useAuthStore();
  const accessToken = tokens?.accessToken;

  const [members, setMembers] = useState<TeamMemberStatus[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeCount, setActiveCount] = useState(0);
  const [trackingCount, setTrackingCount] = useState(0);

  // Convert basic Member to TeamMemberStatus (fallback)
  const mapMemberToStatus = (member: Member): TeamMemberStatus => ({
    userId: member.userId,
    displayName: member.displayName,
    role: member.role,
    status: member.status,
    todayDurationSeconds: 0,
    todayDurationFormatted: '--',
    isTracking: false,
  });

  const loadTeamStatus = useCallback(async () => {
    if (!accessToken) return;
    setIsLoading(true);
    setError(null);
    try {
      // Try the new team status endpoint first
      const response = await getTeamStatus(accessToken);
      setMembers(response.members);
      setActiveCount(response.activeCount);
      setTrackingCount(response.trackingCount);
    } catch (err) {
      console.warn('[useTeamStatus] Team status endpoint failed, falling back to basic members:', err);
      try {
        // Fallback: use basic members list (always works)
        const basicResponse = await listMembers(accessToken);
        const mappedMembers = basicResponse.members.map(mapMemberToStatus);
        setMembers(mappedMembers);
        setActiveCount(mappedMembers.filter(m => m.status === 'Active').length);
        setTrackingCount(0);
      } catch (fallbackErr) {
        setError(err instanceof Error ? err.message : 'Erro ao carregar status da equipe');
      }
    } finally {
      setIsLoading(false);
    }
  }, [accessToken]);

  return {
    members,
    isLoading,
    error,
    activeCount,
    trackingCount,
    loadTeamStatus,
  };
}
