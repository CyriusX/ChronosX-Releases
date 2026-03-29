/**
 * App Categories API — Web version using web apiClient
 */

import { api } from './apiClient';
import type {
  GlobalCategorySearchResponse,
  AppCategoryOverrideListResponse,
  AppCategoryOverrideResponse,
  UpsertAppCategoryOverrideRequest,
  CategoryUsageStatsResponse,
  AppCategoryFilter,
} from '@desktop/types/appCategories';

export async function searchGlobalCategories(
  orgId: string,
  options?: { search?: string; productivity?: AppCategoryFilter; limit?: number }
): Promise<GlobalCategorySearchResponse> {
  const params = new URLSearchParams();
  if (options?.search) params.append('search', options.search);
  if (options?.productivity && options.productivity !== 'all' && options.productivity !== 'overrides')
    params.append('productivity', options.productivity);
  if (options?.limit) params.append('limit', options.limit.toString());
  const qs = params.toString();
  return api.get<GlobalCategorySearchResponse>(`/orgs/${orgId}/app-categories/global${qs ? `?${qs}` : ''}`);
}

export async function getOverrides(
  orgId: string,
  options?: { page?: number; pageSize?: number; productivity?: string }
): Promise<AppCategoryOverrideListResponse> {
  const params = new URLSearchParams();
  if (options?.page) params.append('page', options.page.toString());
  if (options?.pageSize) params.append('pageSize', options.pageSize.toString());
  if (options?.productivity) params.append('productivity', options.productivity);
  const qs = params.toString();
  return api.get<AppCategoryOverrideListResponse>(`/orgs/${orgId}/app-categories/overrides${qs ? `?${qs}` : ''}`);
}

export async function upsertOverride(
  orgId: string,
  request: UpsertAppCategoryOverrideRequest
): Promise<AppCategoryOverrideResponse> {
  return api.put<AppCategoryOverrideResponse>(`/orgs/${orgId}/app-categories/overrides`, request);
}

export async function deleteOverride(orgId: string, identifier: string): Promise<void> {
  return api.delete<void>(`/orgs/${orgId}/app-categories/overrides/${encodeURIComponent(identifier)}`);
}

export async function getUsageStats(
  orgId: string,
  options?: { startDate?: Date; endDate?: Date; limit?: number }
): Promise<CategoryUsageStatsResponse> {
  const params = new URLSearchParams();
  if (options?.startDate) params.append('startDate', options.startDate.toISOString());
  if (options?.endDate) params.append('endDate', options.endDate.toISOString());
  if (options?.limit) params.append('limit', options.limit.toString());
  const qs = params.toString();
  return api.get<CategoryUsageStatsResponse>(`/orgs/${orgId}/app-categories/stats${qs ? `?${qs}` : ''}`);
}
