/**
 * Report API - HTTP client for report endpoints
 *
 * Uses centralized apiClient for automatic 401 handling
 */

import { api } from './apiClient';
import type {
  DailySummaryResponse,
  TopAppsResponse,
  DailySummaryRangeResponse,
  ProductivityTrendResponse,
  TopPathsResponse,
  DistractionStatsResponse,
  CategoryDistributionResponse,
  GroupByOption,
} from '../types/reports';

// ============================================================================
// EXISTING ENDPOINTS
// ============================================================================

/**
 * Get daily summary report
 * GET /api/v1/reports/daily?userId=&date=
 */
export async function getDailySummary(
  date: string,
  userId?: string
): Promise<DailySummaryResponse> {
  const params = new URLSearchParams({ date });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<DailySummaryResponse>(`/reports/daily?${params.toString()}`);
}

/**
 * Get top apps report
 * GET /api/v1/reports/top-apps?userId=&startDate=&endDate=&limit=
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
  });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<TopAppsResponse>(`/reports/top-apps?${params.toString()}`);
}

// ============================================================================
// NEW ENDPOINTS - CX-155 (Página de Relatório)
// ============================================================================

/**
 * Get daily summary range for heatmap
 * GET /api/v1/reports/daily-summary-range?userId=&startDate=&endDate=
 */
export async function getDailySummaryRange(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DailySummaryRangeResponse> {
  const params = new URLSearchParams({ startDate, endDate });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<DailySummaryRangeResponse>(`/reports/daily-summary-range?${params.toString()}`);
}

/**
 * Get productivity trend for stacked bar chart
 * GET /api/v1/reports/productivity-trend?userId=&startDate=&endDate=&groupBy=
 */
export async function getProductivityTrend(
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  userId?: string
): Promise<ProductivityTrendResponse> {
  const params = new URLSearchParams({ startDate, endDate, groupBy });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<ProductivityTrendResponse>(`/reports/productivity-trend?${params.toString()}`);
}

/**
 * Get top paths (URLs and file paths)
 * GET /api/v1/reports/top-paths?userId=&startDate=&endDate=&limit=
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
  });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<TopPathsResponse>(`/reports/top-paths?${params.toString()}`);
}

/**
 * Get distraction statistics
 * GET /api/v1/reports/distraction-stats?userId=&startDate=&endDate=
 */
export async function getDistractionStats(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DistractionStatsResponse> {
  const params = new URLSearchParams({ startDate, endDate });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<DistractionStatsResponse>(`/reports/distraction-stats?${params.toString()}`);
}

/**
 * Get category distribution for donut chart
 * GET /api/v1/reports/category-distribution?userId=&startDate=&endDate=
 */
export async function getCategoryDistribution(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<CategoryDistributionResponse> {
  const params = new URLSearchParams({ startDate, endDate });
  if (userId) {
    params.append('userId', userId);
  }

  return api.get<CategoryDistributionResponse>(`/reports/category-distribution?${params.toString()}`);
}
