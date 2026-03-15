/**
 * useAppCategories - Hook for managing app categories state (CX-144)
 *
 * SOLID:
 * - SRP: Apenas gerenciamento de estado de categorias de apps
 * - OCP: Extensível para novos filtros e ações
 *
 * Composition:
 * - Compõe fetch de dados, filtros e ações CRUD
 *
 * Data flow:
 * 1. Busca apps USADOS na organização (getUsageStats) - fonte principal
 * 2. Busca overrides da organização (getOverrides)
 * 3. Combina para mostrar apps com dados de uso + classificação
 */

import { useState, useEffect, useCallback, useMemo } from 'react';
import type {
  AppCategoryDisplayItem,
  AppCategoryFilter,
  ProductivityCategory,
  AppSubcategory,
  IdentifierType,
} from '../../types/appCategories';
import {
  getUsageStats,
  getOverrides,
  upsertOverride,
  deleteOverride,
} from '../../services/appCategoriesApi';

interface UseAppCategoriesProps {
  accessToken: string | null | undefined;
  orgId: string | null | undefined;
}

interface UseAppCategoriesReturn {
  // Data
  apps: AppCategoryDisplayItem[];
  overridesCount: number;
  uncategorizedCount: number;

  // State
  isLoading: boolean;
  error: string | null;
  searchQuery: string;
  activeFilter: AppCategoryFilter;

  // Actions
  setSearchQuery: (query: string) => void;
  setActiveFilter: (filter: AppCategoryFilter) => void;
  refresh: () => Promise<void>;
  createOverride: (request: OverrideRequest) => Promise<void>;
  removeOverride: (identifier: string) => Promise<void>;
}

export interface OverrideRequest {
  identifier: string;
  identifierType: IdentifierType;
  displayName?: string;
  productivity: ProductivityCategory;
  subcategory: AppSubcategory;
  note?: string;
}

/**
 * Hook for managing app categories with filtering and CRUD operations
 *
 * Data source priority:
 * 1. Usage stats (apps actually used in the org) - PRIMARY
 * 2. Overrides (org-specific classifications)
 * 3. Global categories are applied via the backend's resolver
 */
export function useAppCategories({
  accessToken,
  orgId,
}: UseAppCategoriesProps): UseAppCategoriesReturn {
  // State
  const [allApps, setAllApps] = useState<AppCategoryDisplayItem[]>([]);
  const [overridesCount, setOverridesCount] = useState(0);
  const [uncategorizedCount, setUncategorizedCount] = useState(0);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [searchQuery, setSearchQuery] = useState('');
  const [activeFilter, setActiveFilter] = useState<AppCategoryFilter>('all');

  /**
   * Fetch all data from API
   * Primary source: getUsageStats (apps actually used in the org)
   */
  const fetchData = useCallback(async () => {
    if (!accessToken || !orgId) {
      setIsLoading(false);
      return;
    }

    setIsLoading(true);
    setError(null);

    try {
      // Fetch usage stats and overrides in parallel
      const [statsResponse, overridesResponse] = await Promise.all([
        getUsageStats(accessToken, orgId, { limit: 100 }),
        getOverrides(accessToken, orgId, { pageSize: 100 }),
      ]);

      // Build overrides map for quick lookup
      const overridesMap = new Map(
        overridesResponse.overrides.map((o) => [o.identifier.toLowerCase(), o])
      );

      // Combine ALL apps from usage stats (productive, neutral, distraction, uncategorized)
      const mergedApps: AppCategoryDisplayItem[] = [];
      const addedIdentifiers = new Set<string>();

      // Helper to add app if not already added
      const addAppIfNew = (
        app: {
          identifier: string;
          displayName: string;
          productivity: string;
          subcategory: string;
          source: string;
          totalMinutes?: number;
          sessionCount?: number;
        },
        identifierType: IdentifierType = 'exe'
      ) => {
        const key = app.identifier.toLowerCase();
        if (addedIdentifiers.has(key)) return;
        addedIdentifiers.add(key);

        const override = overridesMap.get(key);
        mergedApps.push({
          identifier: app.identifier,
          identifierType,
          displayName: app.displayName,
          productivity: (override?.productivity ?? app.productivity) as ProductivityCategory,
          subcategory: (override?.subcategory ?? app.subcategory) as AppSubcategory,
          source: override ? 'org_override' : (app.source as 'global' | 'default'),
          note: override?.note,
          totalMinutes: app.totalMinutes,
          sessionCount: app.sessionCount,
          hasOverride: !!override,
        });
      };

      // Add productive apps
      for (const app of statsResponse.topProductiveApps) {
        addAppIfNew(app);
      }

      // Add neutral apps
      for (const app of statsResponse.topNeutralApps) {
        addAppIfNew(app);
      }

      // Add distraction apps
      for (const app of statsResponse.topDistractionApps) {
        addAppIfNew(app);
      }

      // Add uncategorized apps (apps without global classification)
      for (const app of statsResponse.uncategorizedApps) {
        const key = app.identifier.toLowerCase();
        if (addedIdentifiers.has(key)) continue;
        addedIdentifiers.add(key);

        const override = overridesMap.get(key);
        mergedApps.push({
          identifier: app.identifier,
          identifierType: app.identifierType,
          displayName: app.identifier,
          productivity: (override?.productivity ?? 'unknown') as ProductivityCategory,
          subcategory: (override?.subcategory ?? 'unknown') as AppSubcategory,
          source: override ? 'org_override' : 'default',
          note: override?.note,
          totalMinutes: app.totalMinutes,
          sessionCount: app.sessionCount,
          userCount: app.userCount,
          hasOverride: !!override,
        });
      }

      // Sort by total minutes (most used first)
      mergedApps.sort((a, b) => (b.totalMinutes ?? 0) - (a.totalMinutes ?? 0));

      setAllApps(mergedApps);
      setOverridesCount(overridesResponse.totalCount);
      setUncategorizedCount(
        mergedApps.filter((a) => a.productivity === 'unknown' && !a.hasOverride).length
      );
    } catch (err) {
      console.error('[useAppCategories] Error fetching data:', err);
      setError(err instanceof Error ? err.message : 'Erro ao carregar aplicativos');
    } finally {
      setIsLoading(false);
    }
  }, [accessToken, orgId]);

  /**
   * Create or update an override
   */
  const createOverride = useCallback(
    async (request: OverrideRequest) => {
      if (!accessToken || !orgId) {
        throw new Error('Não autorizado');
      }

      await upsertOverride(accessToken, orgId, request);
      await fetchData();
    },
    [accessToken, orgId, fetchData]
  );

  /**
   * Remove an override
   */
  const removeOverride = useCallback(
    async (identifier: string) => {
      if (!accessToken || !orgId) {
        throw new Error('Não autorizado');
      }

      await deleteOverride(accessToken, orgId, identifier);
      await fetchData();
    },
    [accessToken, orgId, fetchData]
  );

  /**
   * Filtered apps based on search and active filter
   */
  const filteredApps = useMemo(() => {
    let result = allApps;

    // Apply search filter
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase();
      result = result.filter(
        (app) =>
          app.displayName.toLowerCase().includes(query) ||
          app.identifier.toLowerCase().includes(query)
      );
    }

    // Apply category filter
    switch (activeFilter) {
      case 'productive':
        result = result.filter((app) => app.productivity === 'productive');
        break;
      case 'neutral':
        result = result.filter((app) => app.productivity === 'neutral');
        break;
      case 'distraction':
        result = result.filter((app) => app.productivity === 'distraction');
        break;
      case 'overrides':
        result = result.filter((app) => app.hasOverride);
        break;
      case 'unknown':
        result = result.filter((app) => app.productivity === 'unknown');
        break;
      case 'all':
      default:
        // No additional filtering
        break;
    }

    return result;
  }, [allApps, searchQuery, activeFilter]);

  // Fetch data on mount and when dependencies change
  useEffect(() => {
    fetchData();
  }, [fetchData]);

  return {
    apps: filteredApps,
    overridesCount,
    uncategorizedCount,
    isLoading,
    error,
    searchQuery,
    activeFilter,
    setSearchQuery,
    setActiveFilter,
    refresh: fetchData,
    createOverride,
    removeOverride,
  };
}
