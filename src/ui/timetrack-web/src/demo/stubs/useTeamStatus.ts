import { useCallback, useMemo, useState } from 'react';
import type { TeamMemberStatus } from '@desktop/types/member';
import { demoTeamMembers } from '@desktop/demo/demoData';

export function useTeamStatus() {
  const [members] = useState<TeamMemberStatus[]>(demoTeamMembers as any);
  const [isLoading, setIsLoading] = useState(false);
  const [lastFetchedAt, setLastFetchedAt] = useState<Date | null>(new Date());

  const activeCount = useMemo(() => members.filter((m: any) => m.status === 'Active').length, [members]);
  const trackingCount = useMemo(() => members.filter((m: any) => m.isTracking).length, [members]);

  const loadTeamStatus = useCallback(async () => {
    setIsLoading(true);
    try {
      setLastFetchedAt(new Date());
    } finally {
      setIsLoading(false);
    }
  }, []);

  return {
    members: members as any,
    isLoading,
    lastFetchedAt,
    activeCount,
    trackingCount,
    loadTeamStatus,
  } as any;
}

