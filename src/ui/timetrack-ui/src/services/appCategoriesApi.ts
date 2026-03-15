/**
 * App Categories API - HTTP client for app categorization (CX-143/CX-144)
 *
 * SOLID:
 * - SRP: Apenas comunicação HTTP com endpoints de categorização
 * - DIP: Retorna tipos definidos em contracts
 *
 * Composition:
 * - Reutiliza helper functions de policyApi
 */

import type {
  GlobalCategorySearchResponse,
  AppCategoryOverrideListResponse,
  AppCategoryOverrideResponse,
  UpsertAppCategoryOverrideRequest,
  CategoryUsageStatsResponse,
  AppCategoryFilter,
} from '../types/appCategories';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

/**
 * Get authorization headers
 */
function getAuthHeaders(accessToken: string): HeadersInit {
  return {
    'Content-Type': 'application/json',
    'Authorization': `Bearer ${accessToken}`,
  };
}

/**
 * Handle HTTP response and extract data or throw error
 */
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

  // Handle 204 No Content
  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}

// ============================================================================
// GLOBAL CATEGORIES
// ============================================================================

/**
 * Search global categories
 * GET /api/v1/orgs/{orgId}/app-categories/global
 */
export async function searchGlobalCategories(
  accessToken: string,
  orgId: string,
  options?: {
    search?: string;
    productivity?: AppCategoryFilter;
    limit?: number;
  }
): Promise<GlobalCategorySearchResponse> {
  const params = new URLSearchParams();

  if (options?.search) {
    params.append('search', options.search);
  }
  if (options?.productivity && options.productivity !== 'all' && options.productivity !== 'overrides') {
    params.append('productivity', options.productivity);
  }
  if (options?.limit) {
    params.append('limit', options.limit.toString());
  }

  const queryString = params.toString();
  const url = `${API_BASE}/orgs/${orgId}/app-categories/global${queryString ? `?${queryString}` : ''}`;

  const response = await fetch(url, {
    method: 'GET',
    headers: getAuthHeaders(accessToken),
  });

  return handleResponse<GlobalCategorySearchResponse>(response);
}

// ============================================================================
// OVERRIDES
// ============================================================================

/**
 * List all category overrides for an organization
 * GET /api/v1/orgs/{orgId}/app-categories/overrides
 */
export async function getOverrides(
  accessToken: string,
  orgId: string,
  options?: {
    page?: number;
    pageSize?: number;
    productivity?: string;
  }
): Promise<AppCategoryOverrideListResponse> {
  const params = new URLSearchParams();

  if (options?.page) {
    params.append('page', options.page.toString());
  }
  if (options?.pageSize) {
    params.append('pageSize', options.pageSize.toString());
  }
  if (options?.productivity) {
    params.append('productivity', options.productivity);
  }

  const queryString = params.toString();
  const url = `${API_BASE}/orgs/${orgId}/app-categories/overrides${queryString ? `?${queryString}` : ''}`;

  const response = await fetch(url, {
    method: 'GET',
    headers: getAuthHeaders(accessToken),
  });

  return handleResponse<AppCategoryOverrideListResponse>(response);
}

/**
 * Create or update a category override
 * PUT /api/v1/orgs/{orgId}/app-categories/overrides
 */
export async function upsertOverride(
  accessToken: string,
  orgId: string,
  request: UpsertAppCategoryOverrideRequest
): Promise<AppCategoryOverrideResponse> {
  const response = await fetch(`${API_BASE}/orgs/${orgId}/app-categories/overrides`, {
    method: 'PUT',
    headers: getAuthHeaders(accessToken),
    body: JSON.stringify(request),
  });

  return handleResponse<AppCategoryOverrideResponse>(response);
}

/**
 * Delete a category override (revert to global)
 * DELETE /api/v1/orgs/{orgId}/app-categories/overrides/{identifier}
 */
export async function deleteOverride(
  accessToken: string,
  orgId: string,
  identifier: string
): Promise<void> {
  const response = await fetch(
    `${API_BASE}/orgs/${orgId}/app-categories/overrides/${encodeURIComponent(identifier)}`,
    {
      method: 'DELETE',
      headers: getAuthHeaders(accessToken),
    }
  );

  return handleResponse<void>(response);
}

// ============================================================================
// STATISTICS
// ============================================================================

/**
 * Get category usage statistics
 * GET /api/v1/orgs/{orgId}/app-categories/stats
 */
export async function getUsageStats(
  accessToken: string,
  orgId: string,
  options?: {
    startDate?: Date;
    endDate?: Date;
    limit?: number;
  }
): Promise<CategoryUsageStatsResponse> {
  const params = new URLSearchParams();

  if (options?.startDate) {
    params.append('startDate', options.startDate.toISOString());
  }
  if (options?.endDate) {
    params.append('endDate', options.endDate.toISOString());
  }
  if (options?.limit) {
    params.append('limit', options.limit.toString());
  }

  const queryString = params.toString();
  const url = `${API_BASE}/orgs/${orgId}/app-categories/stats${queryString ? `?${queryString}` : ''}`;

  const response = await fetch(url, {
    method: 'GET',
    headers: getAuthHeaders(accessToken),
  });

  return handleResponse<CategoryUsageStatsResponse>(response);
}
