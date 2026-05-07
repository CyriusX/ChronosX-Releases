import { useCallback, useMemo, useState } from 'react';
import { demoTeamMembers } from '../demoData';
import type { TeamMemberStatus } from '../../types/member';

export function useTeamStatus() {
  const [members, setMembers] = useState<TeamMemberStatus[]>(demoTeamMembers as any);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [lastFetchedAt, setLastFetchedAt] = useState<Date | null>(new Date());

  const activeCount = useMemo(() => members.filter((m: any) => m.status === 'Active').length, [members]);
  const trackingCount = useMemo(() => members.filter((m: any) => m.isTracking).length, [members]);

  const loadTeamStatus = useCallback(async (_showLoading: boolean = true) => {
    setError(null);
    setIsLoading(_showLoading);
    try {
      // Stable mock; keep the UI responsive.
      setMembers(demoTeamMembers as any);
      setLastFetchedAt(new Date());
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Failed to load team');
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    members: members as any,
    isLoading,
    error,
    activeCount,
    trackingCount,
    lastFetchedAt,
    loadTeamStatus,
  };
}

