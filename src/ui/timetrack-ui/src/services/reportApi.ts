/**
 * Report API - HTTP client for report endpoints
 */

import type { DailySummaryResponse, TopAppsResponse } from '../types/reports';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

async function getAuthHeaders(accessToken: string): Promise<HeadersInit> {
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
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
