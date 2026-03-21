/**
 * useReportsData Hook - Centralized data fetching for Reports page
 *
 * SRP: Apenas gerencia fetch e estado de dados de reports
 * OCP: Extensível para novos tipos de dados
 * DIP: Depende de authStore (abstração) e reportApi (módulo)
 *
 * Composition: Combina múltiplas fontes de dados em um único hook
 */

import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
import { useAuthStore, selectAccessToken } from '../stores/authStore';
import {
  getDailySummaryRange,
  getProductivityTrend,
  getTopApps,
  getTopPaths,
  getDistractionStats,
  getCategoryDistribution,
} from '../services/reportApi';
import type {
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
  TopAppsResponse,
  TopPathsResponse,
  DistractionStatsResponse,
  CategoryDistributionResponse,
  DateRange,
  PeriodPreset,
  GroupByOption,
} from '../types/reports';
import { PERIOD_PRESETS as periodPresets } from '../types/reports';

// Polling interval for automatic refresh (60 seconds)
const POLLING_INTERVAL_MS = 60000;

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
  /** Refresh all data */
  refresh: () => Promise<void>;
  /** Refresh specific data */
  refreshDailySummaryRange: () => Promise<void>;
  refreshProductivityTrend: () => Promise<void>;
  refreshTopApps: () => Promise<void>;
  refreshTopPaths: () => Promise<void>;
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

  const accessToken = useAuthStore(selectAccessToken);

  // Data state
  const [data, setData] = useState<ReportsDataState>({
    dailySummaryRange: null,
    productivityTrend: null,
    topApps: null,
    topPaths: null,
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
  }));

  // Polling refs
  const pollingIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const isFetchingRef = useRef(false);

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

  // ============================================================================
  // DATA FETCHERS
  // ============================================================================

  const refreshDailySummaryRange = useCallback(async () => {
    if (!accessToken || !filters.dateRange.startDate) {
      console.log('[useReportsData] Skipping daily summary - no token or startDate', {
        hasToken: !!accessToken,
        startDate: filters.dateRange.startDate
      });
      return;
    }

    try {
      console.log('[useReportsData] Fetching daily summary range:', {
        startDate: filters.dateRange.startDate,
        endDate: filters.dateRange.endDate,
        userId: filters.userId
      });
      const result = await getDailySummaryRange(
        accessToken,
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );
      console.log('[useReportsData] Daily summary result:', result);
      setData(prev => ({ ...prev, dailySummaryRange: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching daily summary range:', err);
      throw err;
    }
  }, [accessToken, filters.dateRange, filters.userId]);

  const refreshProductivityTrend = useCallback(async () => {
    if (!accessToken || !filters.dateRange.startDate) return;

    try {
      const result = await getProductivityTrend(
        accessToken,
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
  }, [accessToken, filters.dateRange, filters.groupBy, filters.userId]);

  const refreshTopApps = useCallback(async () => {
    if (!accessToken || !filters.dateRange.startDate) return;

    try {
      const result = await getTopApps(
        accessToken,
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
  }, [accessToken, filters.dateRange, filters.topAppsLimit, filters.userId]);

  const refreshTopPaths = useCallback(async () => {
    if (!accessToken || !filters.dateRange.startDate) return;

    try {
      const result = await getTopPaths(
        accessToken,
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
  }, [accessToken, filters.dateRange, filters.topPathsLimit, filters.userId]);

  const refreshDistractionStats = useCallback(async () => {
    if (!accessToken || !filters.dateRange.startDate) return;

    try {
      const result = await getDistractionStats(
        accessToken,
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );
      setData(prev => ({ ...prev, distractionStats: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching distraction stats:', err);
      throw err;
    }
  }, [accessToken, filters.dateRange, filters.userId]);

  const refreshCategoryDistribution = useCallback(async () => {
    if (!accessToken || !filters.dateRange.startDate) return;

    try {
      const result = await getCategoryDistribution(
        accessToken,
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );
      setData(prev => ({ ...prev, categoryDistribution: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching category distribution:', err);
      throw err;
    }
  }, [accessToken, filters.dateRange, filters.userId]);

  // ============================================================================
  // REFRESH ALL
  // ============================================================================

  const refresh = useCallback(async () => {
    console.log('[useReportsData] Refresh called', {
      hasAccessToken: !!accessToken,
      startDate: filters.dateRange.startDate,
      endDate: filters.dateRange.endDate,
      userId: filters.userId
    });

    if (!accessToken || !filters.dateRange.startDate) {
      console.warn('[useReportsData] Skipping refresh - no token or start date');
      return;
    }

    // Prevent concurrent fetches
    if (isFetchingRef.current) {
      console.log('[useReportsData] Skipping refresh - fetch already in progress');
      return;
    }

    isFetchingRef.current = true;
    setIsLoading(true);
    setError(null);

    try {
      await Promise.all([
        refreshDailySummaryRange(),
        refreshProductivityTrend(),
        refreshTopApps(),
        refreshTopPaths(),
        refreshDistractionStats(),
        refreshCategoryDistribution(),
      ]);
      console.log('[useReportsData] All data refreshed successfully');
    } catch (err) {
      console.error('[useReportsData] Error refreshing data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load reports');
    } finally {
      setIsLoading(false);
      isFetchingRef.current = false;
    }
  }, [
    accessToken,
    filters.dateRange.startDate,
    refreshDailySummaryRange,
    refreshProductivityTrend,
    refreshTopApps,
    refreshTopPaths,
    refreshDistractionStats,
    refreshCategoryDistribution,
  ]);

  // ============================================================================
  // AUTO-FETCH ON MOUNT AND FILTER CHANGES
  // ============================================================================

  useEffect(() => {
    if (autoFetch && accessToken && filters.dateRange.startDate) {
      refresh();
    }
  }, [autoFetch, accessToken, filters.dateRange, filters.userId, filters.groupBy]);

  // ============================================================================
  // POLLING - Automatic refresh every 60 seconds
  // ============================================================================

  useEffect(() => {
    if (!autoFetch || !accessToken || !filters.dateRange.startDate) {
      return;
    }

    pollingIntervalRef.current = setInterval(() => {
      // Prevent concurrent fetches
      if (isFetchingRef.current) {
        console.log('[useReportsData] Skipping polling refresh - fetch already in progress');
        return;
      }

      console.log('[useReportsData] Polling refresh triggered');
      refresh();
    }, POLLING_INTERVAL_MS);

    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    };
  }, [autoFetch, accessToken, filters.dateRange.startDate, refresh]);

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
    refresh,
    refreshDailySummaryRange,
    refreshProductivityTrend,
    refreshTopApps,
    refreshTopPaths,
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
      };
    }

    const totalActiveSeconds = dailyRange.days.reduce((sum, d) => sum + d.totalActiveSeconds, 0);
    const totalIdleSeconds = dailyRange.days.reduce((sum, d) => sum + d.totalIdleSeconds, 0);
    const averageProductivityRatio = dailyRange.days.reduce((sum, d) => sum + d.productivityRatio, 0) / dailyRange.days.length;
    const daysWithData = dailyRange.days.filter(d => d.totalActiveSeconds > 0).length;

    return {
      totalActiveSeconds,
      totalIdleSeconds,
      averageProductivityRatio,
      daysWithData,
    };
  }, [data.dailySummaryRange]);
}
