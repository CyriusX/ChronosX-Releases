/**
 * Report API — Re-exports desktop reportApi functions
 * but ensures they use the web apiClient (which uses web authStore).
 *
 * The desktop reportApi imports `api` from its local `./apiClient`,
 * so we need to replicate the functions here with our web apiClient.
 */

import { api } from './apiClient';
import { getUserTimezone } from '@desktop/types/reports';
import type {
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
  TopAppsResponse,
  TopPathsResponse,
  TopFoldersResponse,
  ReportsBundleResponse,
  DistractionStatsResponse,
  CategoryDistributionResponse,
  GroupByOption,
} from '@desktop/types/reports';

export type DailyActivitiesResponse = {
  date: string;
  sessions: Array<{
    processName: string;
    windowTitle?: string;
    appCategory?: string;
    startedAt: string;
    endedAt: string;
    durationSeconds: number;
  }>;
};

export type {
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
  TopAppsResponse,
  TopPathsResponse,
  TopFoldersResponse,
  ReportsBundleResponse,
  DistractionStatsResponse,
  CategoryDistributionResponse,
};

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
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<ReportsBundleResponse>(`/reports/bundle?${params.toString()}`);
}

export async function getDailySummaryRange(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DailySummaryRangeResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<DailySummaryRangeResponse>(`/reports/daily-summary-range?${params.toString()}`);
}

export async function getProductivityTrend(
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  userId?: string
): Promise<ProductivityTrendResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    groupBy,
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<ProductivityTrendResponse>(`/reports/productivity-trend?${params.toString()}`);
}

export async function getTopApps(
  startDate: string,
  endDate: string,
  limit: number = 20,
  userId?: string
): Promise<TopAppsResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    limit: String(limit),
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<TopAppsResponse>(`/reports/top-apps?${params.toString()}`);
}

export async function getTopPaths(
  startDate: string,
  endDate: string,
  limit: number = 20,
  userId?: string
): Promise<TopPathsResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    limit: String(limit),
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<TopPathsResponse>(`/reports/top-paths?${params.toString()}`);
}

export async function getTopFolders(
  startDate: string,
  endDate: string,
  limit: number = 20,
  userId?: string
): Promise<TopFoldersResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    limit: String(limit),
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<TopFoldersResponse>(`/reports/top-folders?${params.toString()}`);
}

export async function getDistractionStats(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DistractionStatsResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<DistractionStatsResponse>(`/reports/distraction-stats?${params.toString()}`);
}

export async function getCategoryDistribution(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<CategoryDistributionResponse> {
  const params = new URLSearchParams({
    startDate,
    endDate,
    timezone: getUserTimezone(),
  });
  if (userId && userId !== 'all') params.append('userId', userId);
  if (userId === 'all') params.append('allTeam', 'true');
  return api.get<CategoryDistributionResponse>(`/reports/category-distribution?${params.toString()}`);
}

export async function getDailyActivities(
  date: string,
  userId?: string
): Promise<DailyActivitiesResponse> {
  const params = new URLSearchParams({
    date,
    timezone: getUserTimezone(),
  });
  if (userId) params.append('userId', userId);
  return api.get<DailyActivitiesResponse>(`/reports/activities?${params.toString()}`);
}
