/**
 * useActivitiesData — Data fetching hook for the Activities page
 *
 * Manages selected date state and fetches summary + activities for that date.
 * Computes day comparison insights from weekly history.
 */

import { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { useIpc } from './useIpc';
import { useAuthStore } from '../stores/authStore';
import { useHiddenAppsStore } from '../stores/hiddenAppsStore';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '../types/ipc';
import { formatDuration } from '../lib/utils';
import { getDailySummaryRange, getDailyActivities, getTopApps } from '../services/reportApi';
import { getMemberSummary } from '../services/memberApi';

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
  const currentUser = useAuthStore(s => s.user);
  const hiddenApps = useHiddenAppsStore(s => s.hiddenApps);
  const [searchParams, setSearchParams] = useSearchParams();

  // Initialize from URL ?date= parameter (e.g., /activities?date=2026-03-25)
  // Also read ?userId= parameter for viewing other users' data
  const [selectedDate, setSelectedDateRaw] = useState<Date>(() => {
    const dateParam = searchParams.get('date');
    if (dateParam) {
      const parsed = new Date(dateParam + 'T00:00:00');
      if (!isNaN(parsed.getTime())) return parsed;
    }
    return new Date();
  });

  const userId = searchParams.get('userId') ?? undefined;

  const [summary, setSummary] = useState<TodaySummaryResponse | null>(null);
  const [activities, setActivities] = useState<ActivityBlock[]>([]);
  const [isLoading, setIsLoading] = useState(true);
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

  // TODAY: use IPC (local SQLite, real-time)
  // PAST DAYS: use backend API (Postgres, authoritative)
  // When userId is present, always use backend API (viewing other user's data)
  const fetchData = useCallback(async () => {
    if (isFetchingRef.current) return;
    isFetchingRef.current = true;
    setIsLoading(true);

    try {
      // Always fetch calendar range from backend for MiniCalendar colors
      const calStart = formatDatePayload(addDays(selectedDate, -15));
      const calEnd = formatDatePayload(new Date()); // up to today
      const calendarPromise = getDailySummaryRange(calStart, calEnd, userId).catch(() => null);

      if (isToday && !userId) {
        // Today + own data: fetch EVERYTHING from cloud in parallel for speed + consistency
        const summaryPromise = currentUser?.id
          ? getMemberSummary(currentUser.id).catch(() => null)
          : Promise.resolve(null);
        const activitiesPromise = getDailyActivities(datePayload).catch(() => null);

        const [cloudSummary, cloudActivities, calResult] = await Promise.all([
          summaryPromise,
          activitiesPromise,
          calendarPromise,
        ]);

        // Set summary from cloud (or fallback to IPC)
        if (cloudSummary) {
          const summary = cloudSummary as unknown as TodaySummaryResponse;
          if (calResult?.days?.length) {
            const dayNames = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
            summary.weeklyHistory = calResult.days.map(d => {
              const dt = new Date(d.date + 'T00:00:00');
              return {
                date: d.date,
                dayName: dayNames[dt.getDay()],
                hours: Math.round((d.totalActiveSeconds / 3600) * 100) / 100,
                isToday: d.date === datePayload,
              };
            });
          }
          setSummary(summary);
        } else if (isConnected) {
          const summaryRes = await sendQuery('getTodaySummary', { date: datePayload });
          if (summaryRes.success && summaryRes.data) {
            setSummary(summaryRes.data as TodaySummaryResponse);
          }
        }

        // Set activities from cloud (or fallback to IPC)
        if (cloudActivities?.sessions?.length) {
          setActivities(convertSessionsToBlocks(cloudActivities.sessions, hiddenApps));
        } else if (isConnected) {
          const activitiesRes = await sendQuery('getRecentActivities', { date: datePayload });
          if (activitiesRes.success && activitiesRes.data) {
            setActivities(
              ((activitiesRes.data as unknown as { activities: ActivityBlock[] }).activities) ?? [],
            );
          }
        }
      } else {
        // Past days OR viewing other user's data: fetch from backend API (authoritative cloud data)
        // Use the SAME endpoints as the Reports page heatmap for consistent numbers:
        // - getDailySummaryRange: same timezone-aware boundaries and classification as heatmap
        //   (fetch 30-day range centered on selected date for MiniCalendar colors)
        // - getTopApps: same override-aware app classification as Reports TopApps
        // - getDailyActivities: for timeline blocks
        const calStart = formatDatePayload(addDays(selectedDate, -15));
        const calEnd = formatDatePayload(addDays(selectedDate, 15));
        const [rangeResult, topAppsResult, activitiesResult] = await Promise.all([
          getDailySummaryRange(calStart, calEnd, userId).catch(() => null),
          getTopApps(datePayload, datePayload, 20, userId).catch(() => null),
          getDailyActivities(datePayload, userId).catch(() => null),
        ]);

        if (rangeResult) {
          // Find the specific day in the range
          const allDays = rangeResult.days ?? [];
          const dayData = allDays.find(d => d.date === datePayload) ?? allDays[0];
          const totalActive = dayData?.totalActiveSeconds ?? 0;
          const totalIdle = dayData?.totalIdleSeconds ?? 0;
          const productivityRatio = dayData?.productivityRatio ?? 0;
          const productiveTime = Math.round(totalActive * productivityRatio);

          // Build weeklyHistory for MiniCalendar colors from the full range
          const dayNames = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sáb'];
          const calendarHistory = allDays.map(d => {
            const dt = new Date(d.date + 'T00:00:00');
            return {
              date: d.date,
              dayName: dayNames[dt.getDay()],
              hours: Math.round((d.totalActiveSeconds / 3600) * 100) / 100,
              isToday: d.date === formatDatePayload(new Date()),
            };
          });

          // Build app list from topApps (uses same classification as Reports page)
          const apps = topAppsResult?.apps ?? [];
          const topApplications = apps.map(a => ({
            name: a.displayName,
            duration: a.totalSeconds,
            percentage: totalActive > 0 ? Math.round((a.totalSeconds / totalActive) * 100 * 10) / 10 : 0,
            productivity: a.productivity ?? 'neutral',
            subcategory: a.subcategory ?? 'unknown',
          }));

          // Build categories from topApps grouped by productivity
          const categoryGroups: Record<string, number> = {};
          for (const a of apps) {
            const prod = a.productivity ?? 'neutral';
            categoryGroups[prod] = (categoryGroups[prod] ?? 0) + a.totalSeconds;
          }
          const categoryColors: Record<string, string> = { productive: '#4ade80', neutral: '#fbbf24', distraction: '#ef4444' };
          const categories = Object.entries(categoryGroups).map(([key, duration]) => ({
            name: key,
            duration,
            percentage: totalActive > 0 ? Math.round((duration / totalActive) * 100 * 10) / 10 : 0,
            color: categoryColors[key] ?? '#94a3b8',
            productivity: key,
          }));

          setSummary({
            totalDuration: totalActive,
            productiveTime,
            idleTime: totalIdle,
            focusTime: productiveTime,
            focusScore: dayData?.focusScore ?? 0,
            sessionsCount: apps.reduce((sum, a) => sum + a.sessionCount, 0),
            topProjects: [],
            topApplications,
            categories,
            weeklyHistory: calendarHistory,
          } as TodaySummaryResponse);
        }

        if (activitiesResult?.sessions) {
          setActivities(convertSessionsToBlocks(activitiesResult.sessions, hiddenApps));
        }
      }
    } catch {
      /* ignore */
    } finally {
      isFetchingRef.current = false;
      setIsLoading(false);
    }
  }, [sendQuery, isConnected, datePayload, isToday, userId, currentUser?.id]);

  // Re-fetch when date changes or connection established
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Poll every 30s only when viewing today AND own data (not viewing another user)
  useEffect(() => {
    if (!isToday || !isConnected || userId) return;
    const interval = setInterval(fetchData, 30000);
    return () => clearInterval(interval);
  }, [isToday, isConnected, fetchData, userId]);

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

// ============================================================================
// HELPERS — Convert backend API responses to the shapes the UI expects
// ============================================================================

function mapCategoryToProductivity(category?: string): string {
  const cat = category?.toLowerCase() ?? '';

  // Productive subcategories (AppSubcategory enum values 1-8)
  if (['development', 'design', 'communication', 'productivity_tools',
       'meetings', 'documentation', 'dev_ops', 'devops', 'finance',
       'productive', 'productivity'].includes(cat))
    return 'productive';

  // Distraction subcategories (AppSubcategory enum values 40-45)
  if (['social_media', 'entertainment', 'gaming', 'news',
       'music_streaming', 'shopping', 'distraction'].includes(cat))
    return 'distraction';

  // Neutral: browser_general, system, unknown, file_manager, utilities, or anything else
  return 'neutral';
}

const APP_PALETTE = [
  '#38bdf8', '#f472b6', '#34d399', '#fb923c', '#a78bfa',
  '#fbbf24', '#22d3ee', '#f87171', '#4ade80', '#e879f9',
];

function convertSessionsToBlocks(
  sessions: Array<{ processName: string; windowTitle?: string; appCategory?: string; startedAt: string; endedAt: string; durationSeconds: number }>,
  hiddenApps: string[] = []
): ActivityBlock[] {
  if (sessions.length === 0) return [];

  // Filter out hidden apps (user-configurable from Settings)
  const hiddenSet = new Set(hiddenApps.map(a => a.toLowerCase()));
  sessions = sessions.filter(s => !hiddenSet.has(s.processName.toLowerCase()));
  if (sessions.length === 0) return [];

  // Assign colors per process
  const colorMap = new Map<string, string>();
  let colorIdx = 0;
  for (const s of sessions) {
    if (!colorMap.has(s.processName)) {
      colorMap.set(s.processName, APP_PALETTE[colorIdx % APP_PALETTE.length]);
      colorIdx++;
    }
  }

  // Merge consecutive sessions for the same process (< 2min gap)
  const blocks: ActivityBlock[] = [];
  let cur = sessions[0];
  let curStart = cur.startedAt;
  let curEnd = cur.endedAt;
  let tabs: { title: string; duration: number; subcategory: string; color: string }[] = [
    { title: cur.windowTitle || cur.processName, duration: cur.durationSeconds, subcategory: cur.appCategory || 'unknown', color: colorMap.get(cur.processName) || '#94a3b8' }
  ];

  const flush = () => {
    const prod = mapCategoryToProductivity(cur.appCategory);
    blocks.push({
      id: `${curStart}-${cur.processName}`,
      name: extractAppName(cur.windowTitle, cur.processName),
      startUtc: curStart,
      endUtc: curEnd,
      duration: Math.round((new Date(curEnd).getTime() - new Date(curStart).getTime()) / 1000),
      productivity: prod,
      subcategory: cur.appCategory || 'unknown',
      color: colorMap.get(cur.processName) || '#94a3b8',
      tabs: groupTabs(tabs),
    });
  };

  for (let i = 1; i < sessions.length; i++) {
    const s = sessions[i];
    const gap = (new Date(s.startedAt).getTime() - new Date(curEnd).getTime()) / 1000;

    if (s.processName === cur.processName && gap < 120) {
      curEnd = s.endedAt > curEnd ? s.endedAt : curEnd;
      tabs.push({ title: s.windowTitle || s.processName, duration: s.durationSeconds, subcategory: s.appCategory || 'unknown', color: colorMap.get(s.processName) || '#94a3b8' });
    } else {
      flush();
      cur = s;
      curStart = s.startedAt;
      curEnd = s.endedAt;
      tabs = [{ title: s.windowTitle || s.processName, duration: s.durationSeconds, subcategory: s.appCategory || 'unknown', color: colorMap.get(s.processName) || '#94a3b8' }];
    }
  }
  flush();

  return blocks;
}

function extractAppName(windowTitle?: string, processName?: string): string {
  if (!windowTitle) return processName || 'Unknown';
  const seps = [' - ', ' — ', ' – '];
  for (const sep of seps) {
    const idx = windowTitle.lastIndexOf(sep);
    if (idx > 0) {
      const suffix = windowTitle.slice(idx + sep.length).trim();
      if (suffix.length > 2 && suffix !== processName) return suffix;
    }
  }
  return processName || windowTitle;
}

function groupTabs(tabs: { title: string; duration: number; subcategory: string; color: string }[]) {
  const map = new Map<string, { title: string; duration: number; subcategory: string; color: string }>();
  for (const t of tabs) {
    const existing = map.get(t.title);
    if (existing) existing.duration += t.duration;
    else map.set(t.title, { ...t });
  }
  return [...map.values()].sort((a, b) => b.duration - a.duration).slice(0, 8);
}
