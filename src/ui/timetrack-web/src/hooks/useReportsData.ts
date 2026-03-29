/**
 * useReportsData Hook — Web Admin Portal version
 *
 * Same as desktop but always uses backend API (no IPC).
 * All data comes from cloud PostgreSQL, never from local agent.
 */

import { useState, useEffect, useCallback, useMemo, useRef } from 'react';
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
} from '@desktop/types/reports';
import { PERIOD_PRESETS as periodPresets } from '@desktop/types/reports';

const POLLING_INTERVAL_MS = 60000;

// ============================================================================
// TYPES
// ============================================================================

export interface ReportsDataState {
  dailySummaryRange: DailySummaryRangeResponse | null;
  productivityTrend: ProductivityTrendResponse | null;
  topApps: TopAppsResponse | null;
  topPaths: TopPathsResponse | null;
  distractionStats: DistractionStatsResponse | null;
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
  initialPeriod?: PeriodPreset;
  initialDateRange?: DateRange;
  userId?: string;
  autoFetch?: boolean;
}

export interface UseReportsDataReturn {
  data: ReportsDataState;
  isLoading: boolean;
  error: string | null;
  filters: ReportsFilters;
  setPeriodPreset: (preset: PeriodPreset) => void;
  setDateRange: (range: DateRange) => void;
  setUserId: (userId: string | undefined) => void;
  setGroupBy: (groupBy: GroupByOption) => void;
  setTopAppsLimit: (limit: number) => void;
  setTopPathsLimit: (limit: number) => void;
  refresh: () => Promise<void>;
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

  const [data, setData] = useState<ReportsDataState>({
    dailySummaryRange: null,
    productivityTrend: null,
    topApps: null,
    topPaths: null,
    distractionStats: null,
    categoryDistribution: null,
  });

  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [filters, setFilters] = useState<ReportsFilters>(() => ({
    periodPreset: initialDateRange ? 'custom' : initialPeriod,
    dateRange: initialDateRange ?? periodPresets[initialPeriod](),
    userId: initialUserId,
    groupBy: 'day' as GroupByOption,
    topAppsLimit: 20,
    topPathsLimit: 20,
  }));

  const pollingIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const isFetchingRef = useRef(false);

  // Filter setters
  const setPeriodPreset = useCallback((preset: PeriodPreset) => {
    const dateRange = periodPresets[preset]();
    setFilters(prev => ({ ...prev, periodPreset: preset, dateRange }));
  }, []);

  const setDateRange = useCallback((range: DateRange) => {
    setFilters(prev => ({ ...prev, periodPreset: 'custom', dateRange: range }));
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

  // Data fetchers — always from backend API (no IPC)
  const refreshDailySummaryRange = useCallback(async () => {
    if (!filters.dateRange.startDate) return;
    try {
      const result = await getDailySummaryRange(
        filters.dateRange.startDate,
        filters.dateRange.endDate,
        filters.userId
      );
      setData(prev => ({ ...prev, dailySummaryRange: result }));
    } catch (err) {
      console.error('[useReportsData] Error fetching daily summary range:', err);
      throw err;
    }
  }, [filters.dateRange, filters.userId]);

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

  // Refresh all
  const refresh = useCallback(async () => {
    if (!filters.dateRange.startDate || isFetchingRef.current) return;

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
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load reports');
    } finally {
      setIsLoading(false);
      isFetchingRef.current = false;
    }
  }, [
    filters.dateRange.startDate,
    refreshDailySummaryRange,
    refreshProductivityTrend,
    refreshTopApps,
    refreshTopPaths,
    refreshDistractionStats,
    refreshCategoryDistribution,
  ]);

  // Auto-fetch on mount and filter changes
  const lastFetchFiltersRef = useRef<string>('');

  useEffect(() => {
    if (!autoFetch || !filters.dateRange.startDate) return;

    const filterKey = `${filters.dateRange.startDate}|${filters.dateRange.endDate}|${filters.userId ?? ''}|${filters.groupBy}`;

    if (lastFetchFiltersRef.current !== filterKey) {
      lastFetchFiltersRef.current = filterKey;
      refresh();
    }
  }, [autoFetch, filters.dateRange, filters.userId, filters.groupBy, refresh]);

  // Polling
  useEffect(() => {
    if (!autoFetch || !filters.dateRange.startDate) return;

    pollingIntervalRef.current = setInterval(() => {
      if (!isFetchingRef.current) refresh();
    }, POLLING_INTERVAL_MS);

    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
        pollingIntervalRef.current = null;
      }
    };
  }, [autoFetch, filters.dateRange.startDate, refresh]);

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

/**
 * Hook to calculate summary statistics from report data
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
    const focusScore = dailyRange.periodFocusScore ?? 0;
    const baseProductivity = dailyRange.periodBaseProductivity ?? 0;

    return {
      totalActiveSeconds,
      totalIdleSeconds,
      averageProductivityRatio: baseProductivity,
      daysWithData,
      focusScore,
      baseProductivity,
    };
  }, [data.dailySummaryRange]);
}
