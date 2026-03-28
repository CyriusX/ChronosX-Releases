/**
 * useActivitiesData — Data fetching hook for the Activities page
 *
 * Manages selected date state and fetches summary + activities for that date.
 * Computes day comparison insights from weekly history.
 */

import { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useIpc } from './useIpc';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../types/ipc';
import { formatDuration } from '../lib/utils';

// ============================================================================
// TYPES
// ============================================================================

export interface ActivityBlock {
  id: string;
  name: string;
  startUtc: string;
  endUtc: string;
  duration: number;
  productivity: string;
  subcategory: string;
  color: string;
  tabs?: { title: string; duration: number; subcategory: string; color: string }[];
}

export interface ActivitiesData {
  // Date state
  selectedDate: Date;
  isToday: boolean;
  setSelectedDate: (date: Date) => void;
  goToPrevDay: () => void;
  goToNextDay: () => void;
  goToToday: () => void;

  // Data
  summary: TodaySummaryResponse | null;
  activities: ActivityBlock[];
  weeklyHistory: WeeklyHistoryItem[];
  isLoading: boolean;

  // Computed insights
  averageHours: number;
  comparisonText: string;
  productivityComparison: string;
}

// ============================================================================
// HELPERS
// ============================================================================

function isSameDay(a: Date, b: Date): boolean {
  return a.getFullYear() === b.getFullYear()
    && a.getMonth() === b.getMonth()
    && a.getDate() === b.getDate();
}

function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

function formatDatePayload(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

// ============================================================================
// HOOK
// ============================================================================

export function useActivitiesData(): ActivitiesData {
  const { sendQuery, isConnected } = useIpc();
  const [searchParams, setSearchParams] = useSearchParams();

  // Initialize from URL ?date= parameter (e.g., /activities?date=2026-03-25)
  const [selectedDate, setSelectedDateRaw] = useState<Date>(() => {
    const dateParam = searchParams.get('date');
    if (dateParam) {
      const parsed = new Date(dateParam + 'T00:00:00');
      if (!isNaN(parsed.getTime())) return parsed;
    }
    return new Date();
  });

  const [summary, setSummary] = useState<TodaySummaryResponse | null>(null);
  const [activities, setActivities] = useState<ActivityBlock[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const isFetchingRef = useRef(false);

  const isToday = isSameDay(selectedDate, new Date());
  const datePayload = formatDatePayload(selectedDate);

  const setSelectedDate = useCallback((date: Date) => {
    setSelectedDateRaw(date);
    setSummary(null);
    setActivities([]);
    // Update URL to reflect the selected date (without full navigation)
    const dateStr = formatDatePayload(date);
    setSearchParams(dateStr === formatDatePayload(new Date()) ? {} : { date: dateStr }, { replace: true });
  }, [setSearchParams]);

  const fetchData = useCallback(async () => {
    if (isFetchingRef.current || !isConnected) return;
    isFetchingRef.current = true;
    setIsLoading(true);

    try {
      const [summaryRes, activitiesRes] = await Promise.all([
        sendQuery('getTodaySummary', { date: datePayload }),
        sendQuery('getRecentActivities', { date: datePayload }),
      ]);

      if (summaryRes.success && summaryRes.data) {
        setSummary(summaryRes.data as TodaySummaryResponse);
      }
      if (activitiesRes.success && activitiesRes.data) {
        setActivities(
          ((activitiesRes.data as unknown as { activities: ActivityBlock[] }).activities) ?? [],
        );
      }
    } catch {
      /* ignore */
    } finally {
      isFetchingRef.current = false;
      setIsLoading(false);
    }
  }, [sendQuery, isConnected, datePayload]);

  // Re-fetch when date changes or connection established
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Poll every 30s only when viewing today
  useEffect(() => {
    if (!isToday || !isConnected) return;
    const interval = setInterval(fetchData, 30000);
    return () => clearInterval(interval);
  }, [isToday, isConnected, fetchData]);

  // Navigation helpers
  const goToPrevDay = useCallback(() => {
    setSelectedDate(addDays(selectedDate, -1));
  }, [selectedDate, setSelectedDate]);

  const goToNextDay = useCallback(() => {
    if (!isToday) setSelectedDate(addDays(selectedDate, 1));
  }, [selectedDate, isToday, setSelectedDate]);

  const goToToday = useCallback(() => {
    setSelectedDate(new Date());
  }, [setSelectedDate]);

  // Compute insights from weeklyHistory
  const weeklyHistory = summary?.weeklyHistory ?? [];

  const { averageHours, comparisonText, productivityComparison } = useMemo(() => {
    const avg = weeklyHistory.length > 0
      ? weeklyHistory.reduce((sum, d) => sum + d.hours, 0) / weeklyHistory.length
      : 0;
    const todayHours = (summary?.totalDuration ?? 0) / 3600;
    const diff = todayHours - avg;

    const compText = avg === 0
      ? ''
      : diff >= 0
        ? `+${formatDuration(Math.round(diff * 3600))} acima da media`
        : `-${formatDuration(Math.round(Math.abs(diff) * 3600))} abaixo da media`;

    const totalTime = summary?.totalDuration ?? 0;
    const productiveTime = summary?.productiveTime ?? 0;
    const prodPct = totalTime > 0 ? Math.round((productiveTime / totalTime) * 100) : 0;
    const prodText = `${prodPct}%`;

    return { averageHours: avg, comparisonText: compText, productivityComparison: prodText };
  }, [weeklyHistory, summary]);

  return {
    selectedDate,
    isToday,
    setSelectedDate,
    goToPrevDay,
    goToNextDay,
    goToToday,
    summary,
    activities,
    weeklyHistory,
    isLoading,
    averageHours,
    comparisonText,
    productivityComparison,
  };
}
