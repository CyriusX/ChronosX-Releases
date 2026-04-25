/**
 * useReportsData Hook - Centralized data fetching for Reports page
 *
 * SRP: Apenas gerencia fetch e estado de dados de reports
 * OCP: Extensível para novos tipos de dados
 * DIP: Depende de authStore (abstração) e reportApi (módulo)
 *
 * Composition: Combina múltiplas fontes de dados em um único hook
 *
 * HYBRID DATA SOURCE:
 * - "Today" uses IPC Local (SQLite) for real-time data, aligned with Dashboard
 * - Past days use Backend API (PostgreSQL) for synchronized data
 * - This ensures consistency between Dashboard, Activities, and Reports pages
 */

import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import {
  getReportsBundle,
  getDailySummaryRange,
  getProductivityTrend,
  getTopApps,
  getTopPaths,
  getDistractionStats,
  getCategoryDistribution,
} from '../services/reportApi';
import { useIpc } from './useIpc';
import type {
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
  TopAppsResponse,
  TopPathsResponse,
  TopFoldersResponse,
  DistractionStatsResponse,
  CategoryDistributionResponse,
  DateRange,
  PeriodPreset,
  GroupByOption,
  DailySummaryDayItem,
} from '../types/reports';
import { PERIOD_PRESETS as periodPresets, toLocalDateStr } from '../types/reports';
import type { TodaySummaryResponse } from '../types/ipc';

// ============================================================================
// TYPES
// ============================================================================

export interface ReportsDataState {
  /** Heatmap data */
  dailySummaryRange: DailySummaryRangeResponse | null;
  /** Productivity trend data */
  productivityTrend: ProductivityTrendResponse | null;
  /** Top apps data */
  topApps: TopAppsResponse | null;
  /** Top paths data */
  topPaths: TopPathsResponse | null;
  /** Top folders data */
  topFolders: TopFoldersResponse | null;
  /** Distraction stats data */
  distractionStats: DistractionStatsResponse | null;
  /** Category distribution data */
  categoryDistribution: CategoryDistributionResponse | null;
}

export interface ReportsFilters {
  periodPreset: PeriodPreset;
  dateRange: DateRange;
  userId?: string;
  groupBy: GroupByOption;
  topAppsLimit: number;
  topPathsLimit: number;
  topFoldersLimit: number;
}

export interface UseReportsDataOptions {
  /** Initial period preset */
  initialPeriod?: PeriodPreset;
  /** Custom date range (overrides initialPeriod if provided) */
  initialDateRange?: DateRange;
  /** User ID for managers/admins */
  userId?: string;
  /** Auto-fetch on mount */
  autoFetch?: boolean;
}

export interface UseReportsDataReturn {
  /** Data state */
  data: ReportsDataState;
  /** Loading state */
  isLoading: boolean;
  /** Error state */
  error: string | null;
  /** Current filters */
  filters: ReportsFilters;
  /** Set period preset */
  setPeriodPreset: (preset: PeriodPreset) => void;
  /** Set custom date range */
  setDateRange: (range: DateRange) => void;
  /** Set user ID (for RBAC) */
  setUserId: (userId: string | undefined) => void;
  /** Set group by option */
  setGroupBy: (groupBy: GroupByOption) => void;
  /** Set top apps limit */
  setTopAppsLimit: (limit: number) => void;
  /** Set top paths limit */
  setTopPathsLimit: (limit: number) => void;
  /** Set top folders limit */
  setTopFoldersLimit: (limit: number) => void;
  /** Refresh all data */
  refresh: () => Promise<void>;
  /** Refresh specific data */
  refreshDailySummaryRange: () => Promise<void>;
  refreshProductivityTrend: () => Promise<void>;
  refreshTopApps: () => Promise<void>;
  refreshTopPaths: () => Promise<void>;
  refreshTopFolders: () => Promise<void>;
  refreshDistractionStats: () => Promise<void>;
  refreshCategoryDistribution: () => Promise<void>;
}

// ============================================================================
// HOOK
// ============================================================================

export function useReportsData(options: UseReportsDataOptions = {}): UseReportsDataReturn {
  const {
    initialPeriod = 'last_30_days',
    initialDateRange,
    userId: initialUserId,
    autoFetch = true,
  } = options;

  // Data state
  const [data, setData] = useState<ReportsDataState>({
    dailySummaryRange: null,
    productivityTrend: null,
    topApps: null,
    topPaths: null,
    topFolders: null,
    distractionStats: null,
    categoryDistribution: null,
  });

  // Loading state
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Filters state
  const [filters, setFilters] = useState<ReportsFilters>(() => ({
    periodPreset: initialDateRange ? 'custom' : initialPeriod,
    dateRange: initialDateRange ?? periodPresets[initialPeriod](),
    userId: initialUserId,
    groupBy: 'day' as GroupByOption,
    topAppsLimit: 20,
    topPathsLimit: 20,
    topFoldersLimit: 20,
  }));

  // Fetch coordination refs
  const isFetchingRef = useRef(false);
  const pendingRefreshRef = useRef(false);
  const refreshImplRef = useRef<() => Promise<void>>(async () => {});

  // ============================================================================
  // FILTER SETTERS
  // ============================================================================

  const setPeriodPreset = useCallback((preset: PeriodPreset) => {
    const dateRange = periodPresets[preset]();
    setFilters(prev => ({
      ...prev,
      periodPreset: preset,
      dateRange,
    }));
  }, []);

  const setDateRange = useCallback((range: DateRange) => {
    setFilters(prev => ({
      ...prev,
      periodPreset: 'custom',
      dateRange: range,
    }));
  }, []);

  const setUserId = useCallback((userId: string | undefined) => {
    setFilters(prev => ({ ...prev, userId }));
  }, []);

  const setGroupBy = useCallback((groupBy: GroupByOption) => {
    setFilters(prev => ({ ...prev, groupBy }));
  }, []);

  const setTopAppsLimit = useCallback((limit: number) => {
    setFilters(prev => ({ ...prev, topAppsLimit: Math.max(1, Math.min(100, limit)) }));
  }, []);

  const setTopPathsLimit = useCallback((limit: number) => {
    setFilters(prev => ({ ...prev, topPathsLimit: Math.max(1, Math.min(100, limit)) }));
  }, []);

  const setTopFoldersLimit = useCallback((limit: number) => {
    setFilters(prev => ({ ...prev, topFoldersLimit: Math.max(1, Math.min(100, limit)) }));
  }, []);

  // ============================================================================
  // IPC FOR LOCAL DATA (Today only)
  // ============================================================================

  const { sendQuery, isConnected } = useIpc();

  // ============================================================================
  // DATA FETCHERS
  // ============================================================================

  /**
   * Converts TodaySummaryResponse (IPC) to DailySummaryDayItem format
   * This allows merging local "today" data with backend historical data
   */
  const convertTodaySummaryToDayItem = useCallback((
    summary: TodaySummaryResponse,
    date: string
  ): DailySummaryDayItem => {
    const totalActiveSeconds = summary.totalDuration ?? 0;
    const productiveSeconds = summary.productiveTime ?? 0;
    const productivityRatio = totalActiveSeconds > 0
      ? productiveSeconds / totalActiveSeconds
      : 0;

    return {
      date,
      totalActiveSeconds,
      totalIdleSeconds: summary.idleTime ?? 0,
      productivityRatio,
      focusScore: summary.focusScore ?? 0,
    };
  }, []);

  const refreshDailySummaryRange = useCallback(async () => {
    if (!filters.dateRange.startDate) {
      console.log('[useReportsData] Skipping daily summary - no startDate');
      return;
    }

    // Only use hybrid approach for own data (not when viewing team member)
    const isViewingOwnData = !filters.userId;
    const today = toLocalDateStr(new Date());
    const includesToday = isViewingOwnData &&
      filters.dateRange.startDate <= today &&
      filters.dateRange.endDate >= today;

    try {
      console.log('[useReportsData] Fetching daily summary range:', {
        startDate: filters.dateRange.startDate,
        endDate: filters.dateRange.endDate,
        userId: filters.userId,
        includesToday,
        isViewingOwnData
      });

      // Fetch from backend API
      const result = await getDailySummaryRange(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );

      // If period includes today and we're viewing own data, replace today with local data
      if (includesToday && isConnected) {
        try {
          console.log('[useReportsData] Fetching today from local IPC for real-time data');
          const todayResponse = await sendQuery('getTodaySummary', { date: today });

          if (todayResponse.success && todayResponse.data) {
            const localTodayItem = convertTodaySummaryToDayItem(todayResponse.data, today);

            // Replace today's data in the days array
            const mergedDays = result.days.map(day =>
              day.date === today ? localTodayItem : day
            );

            // If today wasn't in the backend response, add it
            if (!result.days.some(d => d.date === today)) {
              mergedDays.push(localTodayItem);
              mergedDays.sort((a, b) => a.date.localeCompare(b.date));
            }

            result.days = mergedDays;
            console.log('[useReportsData] Merged local today data:', {
              todayActive: localTodayItem.totalActiveSeconds,
              totalDays: mergedDays.length
            });
          }
        } catch (ipcError) {
          console.warn('[useReportsData] Failed to fetch today from IPC, using backend data:', ipcError);
          // Continue with backend data only
        }
      }

      console.log('[useReportsData] Daily summary result:', result);
      setData(prev => ({ ...prev, dailySummaryRange: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching daily summary range:', err);
      throw err;
    }
  }, [filters.dateRange, filters.userId, isConnected, sendQuery, convertTodaySummaryToDayItem]);

  const refreshProductivityTrend = useCallback(async () => {
    if (!filters.dateRange.startDate) return;

    try {
      const result = await getProductivityTrend(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.groupBy,
        filters.userId
      );
      setData(prev => ({ ...prev, productivityTrend: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching productivity trend:', err);
      throw err;
    }
  }, [filters.dateRange, filters.groupBy, filters.userId]);

  const refreshTopApps = useCallback(async () => {
    if (!filters.dateRange.startDate) return;

    try {
      const result = await getTopApps(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.topAppsLimit,
        filters.userId
      );
      setData(prev => ({ ...prev, topApps: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching top apps:', err);
      throw err;
    }
  }, [filters.dateRange, filters.topAppsLimit, filters.userId]);

  const refreshTopPaths = useCallback(async () => {
    if (!filters.dateRange.startDate) return;

    try {
      const result = await getTopPaths(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.topPathsLimit,
        filters.userId
      );
      setData(prev => ({ ...prev, topPaths: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching top paths:', err);
      throw err;
    }
  }, [filters.dateRange, filters.topPathsLimit, filters.userId]);

  const refreshDistractionStats = useCallback(async () => {
    if (!filters.dateRange.startDate) return;

    try {
      const result = await getDistractionStats(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );
      setData(prev => ({ ...prev, distractionStats: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching distraction stats:', err);
      throw err;
    }
  }, [filters.dateRange, filters.userId]);

  const refreshCategoryDistribution = useCallback(async () => {
    if (!filters.dateRange.startDate) return;

    try {
      const result = await getCategoryDistribution(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );
      setData(prev => ({ ...prev, categoryDistribution: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching category distribution:', err);
      throw err;
    }
  }, [filters.dateRange, filters.userId]);

  // ============================================================================
  // REFRESH ALL
  // ============================================================================

  const refreshImpl = useCallback(async () => {
    console.log('[useReportsData] Refresh called', {
      startDate: filters.dateRange.startDate,
      endDate: filters.dateRange.endDate,
      userId: filters.userId
    });

    if (!filters.dateRange.startDate) {
      console.warn('[useReportsData] Skipping refresh - no start date');
      return;
    }

    // Prevent concurrent fetches (queue a refresh instead of dropping it).
    if (isFetchingRef.current) {
      pendingRefreshRef.current = true;
      console.log('[useReportsData] Queued refresh - fetch already in progress');
      return;
    }

    isFetchingRef.current = true;
    pendingRefreshRef.current = false;
    setIsLoading(true);
    setError(null);

    try {
      // Single-call composite endpoint to avoid client fan-out (429 rate limiting).
      const bundle = await getReportsBundle(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.groupBy,
        {
          topApps: filters.topAppsLimit,
          topPaths: filters.topPathsLimit,
          topFolders: filters.topFoldersLimit,
        },
        filters.userId
      );

      // Hybrid override for today (own data only): replace today's heatmap day with local IPC data.
      const isViewingOwnData = !filters.userId;
      const today = toLocalDateStr(new Date());
      const includesToday = isViewingOwnData &&
        filters.dateRange.startDate <= today &&
        filters.dateRange.endDate >= today;

      if (includesToday && isConnected) {
        try {
          const todayResponse = await sendQuery('getTodaySummary', { date: today });
          if (todayResponse.success && todayResponse.data) {
            const localTodayItem = convertTodaySummaryToDayItem(todayResponse.data, today);
            const mergedDays = bundle.dailySummaryRange.days.map(day =>
              day.date === today ? localTodayItem : day
            );
            if (!bundle.dailySummaryRange.days.some(d => d.date === today)) {
              mergedDays.push(localTodayItem);
              mergedDays.sort((a, b) => a.date.localeCompare(b.date));
            }
            bundle.dailySummaryRange.days = mergedDays;
          }
        } catch (ipcError) {
          console.warn('[useReportsData] Failed to fetch today from IPC for bundle override:', ipcError);
        }
      }

      const failedSections = (bundle.errors ?? [])
        .map(e => e.section)
        .filter(Boolean);
      if (failedSections.length > 0) {
        console.warn('[useReportsData] Bundle returned partial data:', bundle.errors);
        setError(`Some report sections failed to load: ${failedSections.join(', ')}`);
      }

      setData({
        dailySummaryRange: bundle.dailySummaryRange,
        productivityTrend: bundle.productivityTrend,
        topApps: bundle.topApps,
        topPaths: bundle.topPaths,
        topFolders: bundle.topFolders ?? null,
        distractionStats: bundle.distractionStats,
        categoryDistribution: bundle.categoryDistribution,
      });
      console.log('[useReportsData] All data refreshed successfully');
    } catch (err) {
      console.error('[useReportsData] Error refreshing data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load reports');
    } finally {
      setIsLoading(false);
      isFetchingRef.current = false;

      // If a refresh was requested while we were fetching (e.g. syncCompleted / IPC overlay ready),
      // run it once more with the latest filters.
      if (pendingRefreshRef.current) {
        pendingRefreshRef.current = false;
        queueMicrotask(() => {
          void refreshImplRef.current();
        });
      }
    }
  }, [
    filters.dateRange.startDate,
    filters.dateRange.endDate,
    filters.userId,
    filters.groupBy,
    filters.topAppsLimit,
    filters.topPathsLimit,
    filters.topFoldersLimit,
    isConnected,
    sendQuery,
    convertTodaySummaryToDayItem,
  ]);

  useEffect(() => {
    refreshImplRef.current = refreshImpl;
  }, [refreshImpl]);

  const refresh = useCallback(async () => refreshImplRef.current(), []);

  const refreshTopFolders = useCallback(async () => {
    // Single-call bundle refresh; we keep this method for API compatibility with the hook shape.
    await refresh();
  }, [refresh]);

  // ============================================================================
  // AUTO-FETCH ON MOUNT AND FILTER CHANGES
  // ============================================================================

  // Track if we've already done initial fetch with connected IPC
  const lastFetchKeyRef = useRef<string | null>(null);
  const lastFetchHadIpcOverlayRef = useRef(false);

  useEffect(() => {
    if (!autoFetch) return;
    if (!filters.dateRange.startDate) return;

    const today = toLocalDateStr(new Date());
    const includesToday = !filters.userId &&
      filters.dateRange.startDate <= today &&
      filters.dateRange.endDate >= today;
    const hasIpcOverlayNow = includesToday && isConnected;

    // Include all inputs that affect the bundle response.
    const key = [
      filters.dateRange.startDate,
      filters.dateRange.endDate,
      filters.userId ?? '',
      filters.groupBy,
      String(filters.topAppsLimit),
      String(filters.topPathsLimit),
      String(filters.topFoldersLimit),
    ].join('|');

    const isNewKey = lastFetchKeyRef.current !== key;
    const needsOverlayRefetch = !isNewKey && hasIpcOverlayNow && !lastFetchHadIpcOverlayRef.current;

    if (isNewKey || needsOverlayRefetch) {
      lastFetchKeyRef.current = key;
      lastFetchHadIpcOverlayRef.current = hasIpcOverlayNow;
      refresh();
    }
  }, [
    autoFetch,
    filters.dateRange.startDate,
    filters.dateRange.endDate,
    filters.userId,
    filters.groupBy,
    filters.topAppsLimit,
    filters.topPathsLimit,
    filters.topFoldersLimit,
    isConnected,
    refresh,
  ]);

  // Auto-refresh on polling interval, syncCompleted IPC events, and window
  // visibility was intentionally removed. The Reports page now fetches once on
  // mount (and whenever filters change) and leaves updates to the user via the
  // header's manual refresh button.

  // ============================================================================
  // RETURN
  // ============================================================================

  return {
    data,
    isLoading,
    error,
    filters,
    setPeriodPreset,
    setDateRange,
    setUserId,
    setGroupBy,
    setTopAppsLimit,
    setTopPathsLimit,
    setTopFoldersLimit,
    refresh,
    refreshDailySummaryRange,
    refreshProductivityTrend,
    refreshTopApps,
    refreshTopPaths,
    refreshTopFolders,
    refreshDistractionStats,
    refreshCategoryDistribution,
  };
}

// ============================================================================
// HELPER HOOKS
// ============================================================================

/**
 * Hook para calcular estatísticas de resumo a partir dos dados
 */
export function useReportsSummary(data: ReportsDataState) {
  return useMemo(() => {
    const dailyRange = data.dailySummaryRange;

    if (!dailyRange || dailyRange.days.length === 0) {
      return {
        totalActiveSeconds: 0,
        totalIdleSeconds: 0,
        averageProductivityRatio: 0,
        daysWithData: 0,
        focusScore: 0,
        baseProductivity: 0,
      };
    }

    const totalActiveSeconds = dailyRange.days.reduce((sum, d) => sum + d.totalActiveSeconds, 0);
    const totalIdleSeconds = dailyRange.days.reduce((sum, d) => sum + d.totalIdleSeconds, 0);
    const daysWithData = dailyRange.days.filter(d => d.totalActiveSeconds > 0).length;

    // Usar FocusScore agregado do período (vem do backend)
    const focusScore = dailyRange.periodFocusScore ?? 0;
    const baseProductivity = dailyRange.periodBaseProductivity ?? 0;

    return {
      totalActiveSeconds,
      totalIdleSeconds,
      // Manter para compatibilidade, mas usar focusScore como principal
      averageProductivityRatio: baseProductivity,
      daysWithData,
      focusScore,
      baseProductivity,
    };
  }, [data.dailySummaryRange]);
}
