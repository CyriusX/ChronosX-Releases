/**
 * useTeamStatus — Web version that imports from web memberApi
 */

import { useState, useCallback, useEffect, useRef } from 'react';
import { getTeamStatus, listMembers } from '../services/memberApi';
import type { TeamMemberStatus, Member } from '@desktop/types/member';

const TEAM_STATUS_POLL_INTERVAL_MS = 30_000; // refresh every 30s

interface UseTeamStatusReturn {
  members: TeamMemberStatus[];
  isLoading: boolean;
  error: string | null;
  activeCount: number;
  trackingCount: number;
  lastFetchedAt: Date | null;
  loadTeamStatus: () => Promise<void>;
}

const mapMemberToStatus = (member: Member): TeamMemberStatus => ({
  userId: member.userId,
  displayName: member.displayName,
  role: member.role,
  status: member.status,
  todayDurationSeconds: 0,
  todayDurationFormatted: '--',
  isTracking: false,
});

export function useTeamStatus(): UseTeamStatusReturn {
  const [members, setMembers] = useState<TeamMemberStatus[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [activeCount, setActiveCount] = useState(0);
  const [trackingCount, setTrackingCount] = useState(0);
  const [lastFetchedAt, setLastFetchedAt] = useState<Date | null>(null);
  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const loadTeamStatus = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await getTeamStatus();
      setMembers(response.members);
      setActiveCount(response.activeCount);
      setTrackingCount(response.trackingCount);
      setLastFetchedAt(new Date());
    } catch (err) {
      console.warn('[useTeamStatus] Team status failed, falling back to members list:', err);
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
      setIsLoading(false);
    }
  }, []);

  // Auto-poll every 30s so the web portal stays fresh without manual refresh
  useEffect(() => {
    pollRef.current = setInterval(() => {
      loadTeamStatus();
    }, TEAM_STATUS_POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
    };
  }, [loadTeamStatus]);

  return { members, isLoading, error, activeCount, trackingCount, lastFetchedAt, loadTeamStatus };
}
