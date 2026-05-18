import { useState, useCallback, useEffect, useRef } from 'react';
import { getTeamStatus, listMembers } from '../services/memberApi';
import type { TeamMemberStatus, Member } from '../types/member';
import { useIpc } from './useIpc';
import { useAuthStore } from '../stores/authStore';

const TEAM_STATUS_POLL_INTERVAL_MS = 10_000; // near real-time (agent sends immediate heartbeat on transitions)

interface UseTeamStatusReturn {
  members: TeamMemberStatus[];
  isLoading: boolean;
  error: string | null;
  activeCount: number;
  trackingCount: number;
  lastFetchedAt: Date | null;
  loadTeamStatus: (showLoading?: boolean) => Promise<void>;
}

export function useTeamStatus(): UseTeamStatusReturn {
  const [members, setMembers] = useState<TeamMemberStatus[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeCount, setActiveCount] = useState(0);
  const [trackingCount, setTrackingCount] = useState(0);
  const [lastFetchedAt, setLastFetchedAt] = useState<Date | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const { sendQuery } = useIpc();
  const currentUser = useAuthStore(s => s.user);

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

  const loadTeamStatus = useCallback(async (showLoading = true) => {
    if (showLoading) setIsLoading(true);
    setError(null);
    try {
      const response = await getTeamStatus();

      // For the current user on this device, override the cloud value with the local
      // IPC value — the same source used by the Dashboard. This is needed because the
      // cloud can accumulate stale sessions from previous agent runs (they are synced
      // but never cleaned up from the backend), which inflates the cloud total vs. the
      // correctly-merged local value.
      let localUserSeconds: number | null = null;
      if (currentUser?.id) {
        try {
          const ipcResponse = await sendQuery('getTodaySummary');
          if (ipcResponse?.data?.totalDuration != null) {
            localUserSeconds = ipcResponse.data.totalDuration;
          }
        } catch {
          // Fall back to cloud value if IPC is unavailable
        }
      }

      const correctedMembers = response.members.map(member => {
        if (localUserSeconds !== null && member.userId === currentUser?.id) {
          return {
            ...member,
            todayDurationSeconds: localUserSeconds!,
          };
        }
        return member;
      });

      setMembers(correctedMembers);
      setActiveCount(response.activeCount);
      setTrackingCount(response.trackingCount);
      setLastFetchedAt(new Date());
    } catch (err) {
      console.warn('[useTeamStatus] Team status endpoint failed, falling back to basic members:', err);
      try {
        const basicResponse = await listMembers();
        const mappedMembers = basicResponse.members.map(mapMemberToStatus);
        setMembers(mappedMembers);
        setActiveCount(mappedMembers.filter(m => m.status === 'Active').length);
        setTrackingCount(0);
      } catch {
        setError(err instanceof Error ? err.message : 'Erro ao carregar status da equipe');
      }
    } finally {
      if (showLoading) setIsLoading(false);
    }
  }, [sendQuery, currentUser]);

  // Auto-poll to keep the team list fresh (matches agent sync interval)
  useEffect(() => {
    pollRef.current = setInterval(() => {
      loadTeamStatus(false); // silent refresh — no loading spinner
    }, TEAM_STATUS_POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [loadTeamStatus]);

  return {
    members,
    isLoading,
    error,
    activeCount,
    trackingCount,
    lastFetchedAt,
    loadTeamStatus,
  };
}
