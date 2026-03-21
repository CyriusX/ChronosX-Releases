/**
 * Report API - HTTP client for report endpoints
 *
 * SRP: Apenas comunicação HTTP com endpoints de reports
 * OCP: Extensível para novos endpoints
 * DIP: Funções puras que dependem de tokens injetados
 */

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

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

async function getAuthHeaders(accessToken: string): Promise<HeadersInit> {
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
    // Disable cache to always get fresh data
    'Cache-Control': 'no-cache, no-store, must-revalidate',
    'Pragma': 'no-cache',
  };
}

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    let errorMessage = `HTTP ${response.status}`;

    try {
      const errorData = await response.json();
      errorMessage = errorData.message
        || errorData.title
        || errorData.error
        || (typeof errorData === 'string' ? errorData : null)
        || errorMessage;
    } catch {
      // Failed to parse error response
    }

    throw new Error(errorMessage);
  }
  return response.json();
}

// ============================================================================
// EXISTING ENDPOINTS
// ============================================================================

/**
 * Get daily summary report
 * GET /api/v1/reports/daily?userId=&date=
 */
export async function getDailySummary(
  accessToken: string,
  date: string,
  userId?: string
): Promise<DailySummaryResponse> {
  const params = new URLSearchParams({ date });
  if (userId) {
    params.append('userId', userId);
  }

  const response = await fetch(`${API_BASE}/reports/daily?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<DailySummaryResponse>(response);
}

/**
 * Get top apps report
 * GET /api/v1/reports/top-apps?userId=&startDate=&endDate=&limit=
 */
export async function getTopApps(
  accessToken: string,
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

  const response = await fetch(`${API_BASE}/reports/top-apps?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<TopAppsResponse>(response);
}

// ============================================================================
// NEW ENDPOINTS - CX-155 (Página de Relatório)
// ============================================================================

/**
 * Get daily summary range for heatmap
 * GET /api/v1/reports/daily-summary-range?userId=&startDate=&endDate=
 */
export async function getDailySummaryRange(
  accessToken: string,
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DailySummaryRangeResponse> {
  const params = new URLSearchParams({ startDate, endDate });
  if (userId) {
    params.append('userId', userId);
  }

  const response = await fetch(`${API_BASE}/reports/daily-summary-range?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<DailySummaryRangeResponse>(response);
}

/**
 * Get productivity trend for stacked bar chart
 * GET /api/v1/reports/productivity-trend?userId=&startDate=&endDate=&groupBy=
 */
export async function getProductivityTrend(
  accessToken: string,
  startDate: string,
  endDate: string,
  groupBy: GroupByOption = 'day',
  userId?: string
): Promise<ProductivityTrendResponse> {
  const params = new URLSearchParams({ startDate, endDate, groupBy });
  if (userId) {
    params.append('userId', userId);
  }

  const response = await fetch(`${API_BASE}/reports/productivity-trend?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<ProductivityTrendResponse>(response);
}

/**
 * Get top paths (URLs and file paths)
 * GET /api/v1/reports/top-paths?userId=&startDate=&endDate=&limit=
 */
export async function getTopPaths(
  accessToken: string,
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

  const response = await fetch(`${API_BASE}/reports/top-paths?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<TopPathsResponse>(response);
}

/**
 * Get distraction statistics
 * GET /api/v1/reports/distraction-stats?userId=&startDate=&endDate=
 */
export async function getDistractionStats(
  accessToken: string,
  startDate: string,
  endDate: string,
  userId?: string
): Promise<DistractionStatsResponse> {
  const params = new URLSearchParams({ startDate, endDate });
  if (userId) {
    params.append('userId', userId);
  }

  const response = await fetch(`${API_BASE}/reports/distraction-stats?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<DistractionStatsResponse>(response);
}

/**
 * Get category distribution for donut chart
 * GET /api/v1/reports/category-distribution?userId=&startDate=&endDate=
 */
export async function getCategoryDistribution(
  accessToken: string,
  startDate: string,
  endDate: string,
  userId?: string
): Promise<CategoryDistributionResponse> {
  const params = new URLSearchParams({ startDate, endDate });
  if (userId) {
    params.append('userId', userId);
  }

  const response = await fetch(`${API_BASE}/reports/category-distribution?${params.toString()}`, {
    method: 'GET',
    headers: await getAuthHeaders(accessToken),
  });
  return handleResponse<CategoryDistributionResponse>(response);
}
