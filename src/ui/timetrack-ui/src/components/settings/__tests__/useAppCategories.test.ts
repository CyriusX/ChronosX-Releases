/**
 * Tests for useAppCategories hook (CX-144)
 *
 * Tests:
 * - Initial state
 * - Data fetching from usage stats (primary source)
 * - Filtering
 * - CRUD operations
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useAppCategories } from '../useAppCategories';
import * as api from '../../../services/appCategoriesApi';

// Mock the API module
vi.mock('../../../services/appCategoriesApi', () => ({
  getUsageStats: vi.fn(),
  getOverrides: vi.fn(),
  upsertOverride: vi.fn(),
  deleteOverride: vi.fn(),
}));

const mockOrgId = 'test-org-id';

const mockStatsResponse = {
  topProductiveApps: [
    {
      identifier: 'code.exe',
      displayName: 'VS Code',
      productivity: 'productive',
      subcategory: 'development',
      totalMinutes: 1200,
      sessionCount: 50,
      source: 'global',
    },
  ],
  topNeutralApps: [
    {
      identifier: 'notepad.exe',
      displayName: 'Notepad',
      productivity: 'neutral',
      subcategory: 'utilities',
      totalMinutes: 60,
      sessionCount: 10,
      source: 'global',
    },
  ],
  topDistractionApps: [
    {
      identifier: 'youtube.com',
      displayName: 'YouTube',
      productivity: 'distraction',
      subcategory: 'entertainment',
      totalMinutes: 300,
      sessionCount: 15,
      source: 'global',
    },
  ],
  uncategorizedApps: [
    {
      identifier: 'unknown-app.exe',
      identifierType: 'exe',
      totalMinutes: 120,
      sessionCount: 5,
      userCount: 2,
    },
  ],
  totalAppsUsed: 4,
  categorizedApps: 3,
  uncategorizedCount: 1,
};

const mockOverridesResponse = {
  overrides: [
    {
      id: 'override-1',
      identifier: 'whatsapp.exe',
      identifierType: 'exe',
      displayName: 'WhatsApp',
      productivity: 'productive',
      subcategory: 'communication',
      note: 'Used for customer support',
      orgId: mockOrgId,
      createdBy: 'user-1',
      createdByName: 'Admin',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
    },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 100,
};

describe('useAppCategories', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.getUsageStats as any).mockResolvedValue(mockStatsResponse);
    (api.getOverrides as any).mockResolvedValue(mockOverridesResponse);
  });

  describe('initialization', () => {
    it('should start with loading state', () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      expect(result.current.isLoading).toBe(true);
      expect(result.current.apps).toEqual([]);
      expect(result.current.error).toBeNull();
    });

    it('should fetch data on mount', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(api.getUsageStats).toHaveBeenCalledWith(
        mockOrgId,
        { limit: 100 }
      );
      expect(api.getOverrides).toHaveBeenCalledWith(
        mockOrgId,
        { pageSize: 100 }
      );
    });

    it('should handle null orgId', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: null })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(api.getUsageStats).not.toHaveBeenCalled();
    });
  });

  describe('data merging', () => {
    it('should combine apps from usage stats', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      // Should have apps from productive, neutral, distraction, and uncategorized
      expect(result.current.apps.length).toBeGreaterThanOrEqual(3);
    });

    it('should include uncategorized apps', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      const unknownApp = result.current.apps.find((a) => a.identifier === 'unknown-app.exe');
      expect(unknownApp).toBeDefined();
      expect(unknownApp?.productivity).toBe('unknown');
    });

    it('should count overrides and uncategorized', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.overridesCount).toBe(1);
      expect(result.current.uncategorizedCount).toBeGreaterThan(0);
    });
  });

  describe('filtering', () => {
    it('should filter by search query', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      act(() => {
        result.current.setSearchQuery('code');
      });

      expect(result.current.apps.length).toBeGreaterThanOrEqual(1);
      expect(result.current.apps.some((a) => a.identifier.includes('code'))).toBe(true);
    });

    it('should filter by productivity', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      act(() => {
        result.current.setActiveFilter('distraction');
      });

      expect(result.current.apps.every((a) => a.productivity === 'distraction')).toBe(true);
    });

    it('should filter overrides only', async () => {
      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      act(() => {
        result.current.setActiveFilter('overrides');
      });

      // Should only show apps with hasOverride=true
      expect(result.current.apps.every((a) => a.hasOverride)).toBe(true);
    });
  });

  describe('CRUD operations', () => {
    it('should create override', async () => {
      (api.upsertOverride as any).mockResolvedValueOnce({});

      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      await act(async () => {
        await result.current.createOverride({
          identifier: 'new-app.exe',
          identifierType: 'exe',
          displayName: 'New App',
          productivity: 'productive',
          subcategory: 'productivity_tools',
        });
      });

      expect(api.upsertOverride).toHaveBeenCalled();
      // Should refresh data after creating
      expect(api.getUsageStats).toHaveBeenCalledTimes(2);
    });

    it('should remove override', async () => {
      (api.deleteOverride as any).mockResolvedValueOnce(undefined);

      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      await act(async () => {
        await result.current.removeOverride('whatsapp.exe');
      });

      expect(api.deleteOverride).toHaveBeenCalledWith(
        mockOrgId,
        'whatsapp.exe'
      );
    });
  });

  describe('error handling', () => {
    it('should handle API errors', async () => {
      (api.getUsageStats as any).mockRejectedValueOnce(
        new Error('Network error')
      );

      const { result } = renderHook(() =>
        useAppCategories({ orgId: mockOrgId })
      );

      await waitFor(() => {
        expect(result.current.isLoading).toBe(false);
      });

      expect(result.current.error).toBe('Network error');
    });
  });
});
