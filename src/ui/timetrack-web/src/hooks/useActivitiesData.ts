/**
 * useActivitiesData — Web Admin Portal version
 *
 * Always fetches from backend API (no IPC).
 * Always requires a userId parameter (viewing team members).
 */

import { useState, useCallback, useEffect, useRef, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { formatDuration } from '@desktop/lib/utils';
import { getDailySummaryRange, getDailyActivities, getTopApps } from '../services/reportApi';
import type { TodaySummaryResponse, WeeklyHistoryItem } from '@desktop/types/ipc';
import type { DailySummaryRangeResponse, TopAppsResponse, DailyActivitiesResponse } from '../services/reportApi';

// ============================================================================
// TYPES
// ============================================================================

export interface ActivityBlock {
  id: string;
  kind?: 'activity' | 'idle';
  name: string;
  startUtc: string;
  endUtc: string;
  duration: number;
  productivity: string;
  subcategory: string;
  color: string;
  reasonCode?: string;
  note?: string;
  submittedAtUtc?: string;
  tabs?: { title: string; duration: number; subcategory: string; color: string }[];
}

export interface ActivitiesData {
  selectedDate: Date;
  isToday: boolean;
  setSelectedDate: (date: Date) => void;
  goToPrevDay: () => void;
  goToNextDay: () => void;
  goToToday: () => void;
  summary: TodaySummaryResponse | null;
  activities: ActivityBlock[];
  weeklyHistory: WeeklyHistoryItem[];
  isLoading: boolean;
  averageHours: number;
  comparisonText: string;
  productivityComparison: string;
  // Web-specific
  selectedUserId: string | undefined;
  setSelectedUserId: (userId: string | undefined) => void;
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
  const [searchParams, setSearchParams] = useSearchParams();

  const [selectedDate, setSelectedDateRaw] = useState<Date>(() => {
    const dateParam = searchParams.get('date');
    if (dateParam) {
      const parsed = new Date(dateParam + 'T00:00:00');
      if (!isNaN(parsed.getTime())) return parsed;
    }
    return new Date();
  });

  const [selectedUserId, setSelectedUserId] = useState<string | undefined>(
    searchParams.get('userId') ?? undefined
  );

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
    const dateStr = formatDatePayload(date);
    const params: Record<string, string> = {};
    if (dateStr !== formatDatePayload(new Date())) params.date = dateStr;
    if (selectedUserId) params.userId = selectedUserId;
    setSearchParams(params, { replace: true });
  }, [setSearchParams, selectedUserId]);

  // Always fetch from backend API (no IPC in web portal)
  const fetchData = useCallback(async () => {
    if (isFetchingRef.current) return;
    isFetchingRef.current = true;
    setIsLoading(true);

    try {
      const calStart = formatDatePayload(addDays(selectedDate, -15));
      const calEnd = formatDatePayload(addDays(selectedDate, 15));

      const [rangeResult, topAppsResult, activitiesResult] = await Promise.all([
        getDailySummaryRange(calStart, calEnd, selectedUserId).catch((): DailySummaryRangeResponse | null => null),
        getTopApps(datePayload, datePayload, 20, selectedUserId).catch((): TopAppsResponse | null => null),
        getDailyActivities(datePayload, selectedUserId).catch((): DailyActivitiesResponse | null => null),
      ]);

      if (rangeResult) {
        const allDays = rangeResult.days ?? [];
        const dayData = allDays.find(d => d.date === datePayload) ?? allDays[0];
        const totalActive = dayData?.totalActiveSeconds ?? 0;
        const totalIdle = dayData?.totalIdleSeconds ?? 0;
        const productivityRatio = dayData?.productivityRatio ?? 0;
        const productiveTime = Math.round(totalActive * productivityRatio);

        const dayNames = ['Dom', 'Seg', 'Ter', 'Qua', 'Qui', 'Sex', 'Sab'];
        const calendarHistory = allDays.map(d => {
          const dt = new Date(d.date + 'T00:00:00');
          return {
            date: d.date,
            dayName: dayNames[dt.getDay()],
            hours: Math.round((d.totalActiveSeconds / 3600) * 100) / 100,
            isToday: d.date === formatDatePayload(new Date()),
          };
        });

        const apps = topAppsResult?.apps ?? [];
        const topApplications = apps.map(a => ({
          name: a.displayName,
          duration: a.totalSeconds,
          percentage: totalActive > 0 ? Math.round((a.totalSeconds / totalActive) * 100 * 10) / 10 : 0,
          productivity: a.productivity ?? 'neutral',
          subcategory: a.subcategory ?? 'unknown',
        }));

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
        setActivities(
          convertActivitiesToBlocks(
            activitiesResult.sessions,
            activitiesResult.idlePeriods ?? []
          )
        );
      }
    } catch {
      /* ignore */
    } finally {
      isFetchingRef.current = false;
      setIsLoading(false);
    }
  }, [datePayload, selectedUserId, selectedDate]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // Navigation
  const goToPrevDay = useCallback(() => {
    setSelectedDate(addDays(selectedDate, -1));
  }, [selectedDate, setSelectedDate]);

  const goToNextDay = useCallback(() => {
    if (!isToday) setSelectedDate(addDays(selectedDate, 1));
  }, [selectedDate, isToday, setSelectedDate]);

  const goToToday = useCallback(() => {
    setSelectedDate(new Date());
  }, [setSelectedDate]);

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

    return { averageHours: avg, comparisonText: compText, productivityComparison: `${prodPct}%` };
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
    selectedUserId,
    setSelectedUserId,
  };
}

// ============================================================================
// HELPERS
// ============================================================================

function mapCategoryToProductivity(category?: string): string {
  const cat = category?.toLowerCase() ?? '';
  if (['development', 'design', 'communication', 'productivity_tools',
       'meetings', 'documentation', 'dev_ops', 'devops', 'finance',
       'productive', 'productivity'].includes(cat))
    return 'productive';
  if (['social_media', 'entertainment', 'gaming', 'news',
       'music_streaming', 'shopping', 'distraction'].includes(cat))
    return 'distraction';
  return 'neutral';
}

const APP_PALETTE = [
  '#38bdf8', '#f472b6', '#34d399', '#fb923c', '#a78bfa',
  '#fbbf24', '#22d3ee', '#f87171', '#4ade80', '#e879f9',
];

function convertActivitiesToBlocks(
  sessions: Array<{ processName: string; windowTitle?: string; appCategory?: string; startedAt: string; endedAt: string; durationSeconds: number }>
  ,idlePeriods: Array<{ id: string; startedAt: string; endedAt: string; durationSeconds: number; reasonCode?: string; note?: string; submittedAtUtc?: string }>
): ActivityBlock[] {
  const blocks: ActivityBlock[] = [];
  if (sessions.length === 0 && idlePeriods.length === 0) return blocks;

  const colorMap = new Map<string, string>();
  let colorIdx = 0;
  for (const s of sessions) {
    if (!colorMap.has(s.processName)) {
      colorMap.set(s.processName, APP_PALETTE[colorIdx % APP_PALETTE.length]);
      colorIdx++;
    }
  }

  if (sessions.length > 0) {
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
        kind: 'activity',
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
  }

  for (const idlePeriod of idlePeriods) {
    blocks.push({
      id: idlePeriod.id,
      kind: 'idle',
      name: 'Idle',
      startUtc: idlePeriod.startedAt,
      endUtc: idlePeriod.endedAt,
      duration: idlePeriod.durationSeconds,
      productivity: 'idle',
      subcategory: 'idle',
      color: '#64748b',
      reasonCode: idlePeriod.reasonCode,
      note: idlePeriod.note,
      submittedAtUtc: idlePeriod.submittedAtUtc,
      tabs: [],
    });
  }

  return blocks.sort((a, b) => new Date(a.startUtc).getTime() - new Date(b.startUtc).getTime());
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
