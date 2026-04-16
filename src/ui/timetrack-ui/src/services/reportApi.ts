/**
 * Report API - HTTP client for report endpoints
 *
 * Uses centralized apiClient for automatic 401 handling.
 * All date-range endpoints send the user's IANA timezone so the backend
 * can compute correct UTC boundaries for "local day" queries.
 */

import { api } from './apiClient';
import { getUserTimezone } from '../types/reports';
import type {
  DailySummaryResponse,
  TopAppsResponse,
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
  TopPathsResponse,
  TopFoldersResponse,
  ReportsBundleResponse,
  DistractionStatsResponse,
  CategoryDistributionResponse,
  GroupByOption,
} from '../types/reports';

// ============================================================================
// EXISTING ENDPOINTS
// ============================================================================

/**
 * Get daily summary report
 * GET /api/v1/reports/daily?userId=&date=&timezone=
 */
export async function getDailySummary(
  date: string,
  userId?: string
): Promise<DailySummaryResponse> {
  const params = new URLSearchParams({ date, timezone: getUserTimezone() });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<DailySummaryResponse>(`/reports/daily?${params.toString()}`);
}

/**
 * Get daily activities (individual sessions for timeline display)
 * GET /api/v1/reports/activities?userId=&date=&timezone=
 */
export async function getDailyActivities(
  date: string,
  userId?: string
): Promise<{ date: string; sessions: Array<{ processName: string; windowTitle?: string; appCategory?: string; startedAt: string; endedAt: string; durationSeconds: number }> }> {
  const params = new URLSearchParams({ date, timezone: getUserTimezone() });
  if (userId) {
    params.append('userId', userId);
  }
  return api.get(`/reports/activities?${params.toString()}`);
}

/**
 * Get top apps report
 * GET /api/v1/reports/top-apps?userId=&startDate=&endDate=&limit=&timezone=
 */
export async function getTopApps(
  startDate: string,
  endDate: string,
  limit: number = 10,
  userId?: string
): Promise<TopAppsResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    limit: limit.toString(),
    timezone: getUserTimezone(),
  });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<TopAppsResponse>(`/reports/top-apps?${params.toString()}`);
}

// ============================================================================
// NEW ENDPOINTS - CX-155 (Página de Relatório)
// ============================================================================

/**
 * Get reports bundle (single request for the Reports page)
 * GET /api/v1/reports/bundle?userId=&startDate=&endDate=&groupBy=&limits=&timezone=
 */
export async function getReportsBundle(
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  limits: { topApps?: number; topPaths?: number; topFolders?: number } = {},
  userId?: string
): Promise<ReportsBundleResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    groupBy,
    timezone: getUserTimezone(),
    topAppsLimit: String(limits.topApps ?? 20),
    topPathsLimit: String(limits.topPaths ?? 20),
    topFoldersLimit: String(limits.topFolders ?? 20),
  });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<ReportsBundleResponse>(`/reports/bundle?${params.toString()}`);
}

/**
 * Get daily summary range for heatmap
 * GET /api/v1/reports/daily-summary-range?userId=&startDate=&endDate=&timezone=
 */
export async function getDailySummaryRange(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DailySummaryRangeResponse> {
  const params = new URLSearchParams({ startDate, endDate, timezone: getUserTimezone() });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<DailySummaryRangeResponse>(`/reports/daily-summary-range?${params.toString()}`);
}

/**
 * Get productivity trend for stacked bar chart
 * GET /api/v1/reports/productivity-trend?userId=&startDate=&endDate=&groupBy=&timezone=
 */
export async function getProductivityTrend(
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  userId?: string
): Promise<ProductivityTrendResponse> {
  const params = new URLSearchParams({ startDate, endDate, groupBy, timezone: getUserTimezone() });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<ProductivityTrendResponse>(`/reports/productivity-trend?${params.toString()}`);
}

/**
 * Get top paths (URLs and file paths)
 * GET /api/v1/reports/top-paths?userId=&startDate=&endDate=&limit=&timezone=
 */
export async function getTopPaths(
  startDate: string,
  endDate: string,
  limit: number = 20,
  userId?: string
): Promise<TopPathsResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    limit: limit.toString(),
    timezone: getUserTimezone(),
  });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<TopPathsResponse>(`/reports/top-paths?${params.toString()}`);
}

/**
 * Get top folders (file system paths)
 * GET /api/v1/reports/top-folders?userId=&startDate=&endDate=&limit=&timezone=
 */
export async function getTopFolders(
  startDate: string,
  endDate: string,
  limit: number = 20,
  userId?: string
): Promise<TopFoldersResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    limit: limit.toString(),
    timezone: getUserTimezone(),
  });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }
  return api.get<TopFoldersResponse>(`/reports/top-folders?${params.toString()}`);
}

/**
 * Get distraction statistics
 * GET /api/v1/reports/distraction-stats?userId=&startDate=&endDate=&timezone=
 */
export async function getDistractionStats(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DistractionStatsResponse> {
  const params = new URLSearchParams({ startDate, endDate, timezone: getUserTimezone() });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<DistractionStatsResponse>(`/reports/distraction-stats?${params.toString()}`);
}

/**
 * Get category distribution for donut chart
 * GET /api/v1/reports/category-distribution?userId=&startDate=&endDate=&timezone=
 */
export async function getCategoryDistribution(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<CategoryDistributionResponse> {
  const params = new URLSearchParams({ startDate, endDate, timezone: getUserTimezone() });
  if (userId === 'all') {
    params.append('allTeam', 'true');
  } else if (userId) {
    params.append('userId', userId);
  }

  return api.get<CategoryDistributionResponse>(`/reports/category-distribution?${params.toString()}`);
}
