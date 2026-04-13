import { useState, useCallback, useEffect, useRef } from 'react';
import { getTeamStatus, listMembers } from '../services/memberApi';
import { getDailySummaryRange } from '../services/reportApi';
import type { TeamMemberStatus, Member } from '../types/member';

const TEAM_STATUS_POLL_INTERVAL_MS = 30_000; // refresh every 30s (matches agent sync interval)

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

      // Enrich with correct duration from /reports/daily-summary-range
      // (same data source the web UI uses — verified accurate)
      const today = new Date();
      const todayStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;

      let correctedMembers = response.members;
      try {
        const summaryResults = await Promise.allSettled(
          response.members.map(m => getDailySummaryRange(todayStr, todayStr, m.userId))
        );

        correctedMembers = response.members.map((member, i) => {
          const result = summaryResults[i];
          if (result.status === 'fulfilled') {
            const dayData = result.value.days?.find((d: { date: string }) => d.date === todayStr) ?? result.value.days?.[0];
            if (dayData) {
              const cloudSecs = dayData.totalActiveSeconds;
              const originalSecs = member.todayDurationSeconds;
              console.log(
                `[useTeamStatus] CLOUD totalActiveSeconds=${cloudSecs}s (${Math.floor(cloudSecs/3600)}h${Math.floor((cloudSecs%3600)/60)}m) | getTeamStatus=${originalSecs}s | member=${member.displayName}`
              );
              return {
                ...member,
                todayDurationSeconds: dayData.totalActiveSeconds,
                productivityRatio: dayData.productivityRatio,
              };
            }
            console.warn(`[useTeamStatus] No dayData for ${todayStr}, days=`, result.value.days?.map((d: {date: string}) => d.date));
          }
          if (result.status === 'rejected') {
            console.error(`[useTeamStatus] enrichment failed for ${member.displayName}:`, result.reason);
          }
          return member;
        });
      } catch (enrichErr) {
        console.error('[useTeamStatus] enrichment batch failed:', enrichErr);
      }

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
  }, []);

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
