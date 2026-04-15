/**
 * useTeamStatus — Web version that imports from web memberApi
 */

import { useState, useCallback, useEffect, useRef } from 'react';
import { getTeamStatus, listMembers } from '../services/memberApi';
import { getDailySummaryRange } from '../services/reportApi';
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

      // Enrich with productivityRatio from /reports/daily-summary-range
      const today = new Date();
      const todayStr = `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, '0')}-${String(today.getDate()).padStart(2, '0')}`;

      const summaryResults = await Promise.allSettled(
        response.members.map(m => getDailySummaryRange(todayStr, todayStr, m.userId))
      );

      // Only enrich with productivityRatio — do NOT overwrite todayDurationSeconds.
      // getTeamStatus uses merged intervals for todayDurationSeconds (authoritative value).
      // getDailySummaryRange is only used here for productivityRatio enrichment.
      const correctedMembers = response.members.map((member, i) => {
        const result = summaryResults[i];
        if (result.status === 'fulfilled') {
          const dayData = result.value.days?.find((d: { date: string }) => d.date === todayStr) ?? result.value.days?.[0];
          if (dayData) {
            return {
              ...member,
              productivityRatio: dayData.productivityRatio,
            };
          }
        }
        return member;
      });

      setMembers(correctedMembers);
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
