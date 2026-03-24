/**
 * App Categories API - HTTP client for app categorization (CX-143/CX-144)
 *
 * Uses centralized apiClient for automatic 401 handling
 */

import { api } from './apiClient';
import type {
  GlobalCategorySearchResponse,
  AppCategoryOverrideListResponse,
  AppCategoryOverrideResponse,
  UpsertAppCategoryOverrideRequest,
  CategoryUsageStatsResponse,
  AppCategoryFilter,
} from '../types/appCategories';

// ============================================================================
// GLOBAL CATEGORIES
// ============================================================================

/**
 * Search global categories
 * GET /api/v1/orgs/{orgId}/app-categories/global
 */
export async function searchGlobalCategories(
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
  const endpoint = `/orgs/${orgId}/app-categories/global${queryString ? `?${queryString}` : ''}`;

  return api.get<GlobalCategorySearchResponse>(endpoint);
}

// ============================================================================
// OVERRIDES
// ============================================================================

/**
 * List all category overrides for an organization
 * GET /api/v1/orgs/{orgId}/app-categories/overrides
 */
export async function getOverrides(
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
  const endpoint = `/orgs/${orgId}/app-categories/overrides${queryString ? `?${queryString}` : ''}`;

  return api.get<AppCategoryOverrideListResponse>(endpoint);
}

/**
 * Create or update a category override
 * PUT /api/v1/orgs/{orgId}/app-categories/overrides
 */
export async function upsertOverride(
  orgId: string,
  request: UpsertAppCategoryOverrideRequest
): Promise<AppCategoryOverrideResponse> {
  return api.put<AppCategoryOverrideResponse>(`/orgs/${orgId}/app-categories/overrides`, request);
}

/**
 * Delete a category override (revert to global)
 * DELETE /api/v1/orgs/{orgId}/app-categories/overrides/{identifier}
 */
export async function deleteOverride(
  orgId: string,
  identifier: string
): Promise<void> {
  return api.delete<void>(`/orgs/${orgId}/app-categories/overrides/${encodeURIComponent(identifier)}`);
}

// ============================================================================
// STATISTICS
// ============================================================================

/**
 * Get category usage statistics
 * GET /api/v1/orgs/{orgId}/app-categories/stats
 */
export async function getUsageStats(
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
  const endpoint = `/orgs/${orgId}/app-categories/stats${queryString ? `?${queryString}` : ''}`;

  return api.get<CategoryUsageStatsResponse>(endpoint);
}
