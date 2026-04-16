/**
 * useDashboardData Hook - Manages dashboard data fetching and real-time updates
 *
 * SOLID:
 * - SRP: Only manages dashboard data lifecycle
 * - DIP: Depends on useIpc abstraction
 * - OCP: Extensible via new data sources
 */

import { useEffect, useCallback, useRef } from 'react';
import { useTrackingStore, handleTrackingStateChanged, handleSessionUpdated } from '../stores/trackingStore';
import { useAuthStore } from '../stores/authStore';
import { useIpc } from './useIpc';
import { getMySummary } from '../services/memberApi';
import { isDesktopRuntime } from '../lib/runtime';
import type { TodaySummaryResponse, TrackingStateResponse, SyncStateResponse } from '../types/ipc';

const POLLING_INTERVAL_MS = 5000; // 5 seconds — keeps dashboard live without waiting for sessionUpdated events
const WEB_POLLING_INTERVAL_MS = 60_000;

/**
 * Hook that manages dashboard data fetching, polling, and event subscriptions
 *
 * Features:
 * - Initial data fetch on connection
 * - 60-second polling for summary data
 * - Real-time updates via IPC events
 * - Automatic refresh on connection restore
 */
export function useDashboardData() {
  const { isConnected, sendQuery, subscribeToEvent, sendCommand } = useIpc();
  const currentUser = useAuthStore(s => s.user);
  const desktopRuntime = isDesktopRuntime();

  const {
    setTrackingState,
    setTodaySummary,
    setSyncState,
    setPaused,
    todaySummary,
    isPaused,
    isTracking,
    weeklyHistory,
    categories,
    syncState,
  } = useTrackingStore();

  const pollingIntervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
  const isFetchingRef = useRef(false);

  // ============================================================================
  // DATA FETCHING
  // ============================================================================

  const fetchDashboardData = useCallback(async () => {
    // Prevent concurrent fetches
    if (isFetchingRef.current) return;
    isFetchingRef.current = true;

    try {
      // WebUI: there is no IPC. Load backend summary and keep tracking "stopped".
      if (!desktopRuntime) {
        if (currentUser?.id) {
          try {
            const cloudSummary = await getMySummary();
            if (cloudSummary) {
              setTodaySummary(cloudSummary as unknown as TodaySummaryResponse);
            }
          } catch {
            // Backend unavailable — keep whatever is currently shown
          }
        }

        setTrackingState({
          isTracking: false,
          isPaused: false,
          isFocusMode: false,
        });

        return;
      }

      // Desktop runtime: wait until IPC is connected.
      if (!isConnected) return;

      // Fetch tracking state
      const stateResponse = await sendQuery('getTrackingState');
      if (stateResponse.success && stateResponse.data) {
        setTrackingState(stateResponse.data as TrackingStateResponse);
      }

      // Fetch today's summary — IPC (local SQLite) is the source of truth for today.
      // Cloud API only has data after sync, which may lag or be unavailable.
      // We try cloud AFTER local to merge weekly history from the backend.
      const summaryResponse = await sendQuery('getTodaySummary');
      if (summaryResponse.success && summaryResponse.data) {
        setTodaySummary(summaryResponse.data as TodaySummaryResponse);
      }

      // Optionally enrich with cloud data (e.g., weekly history) without overriding local
      if (currentUser?.id) {
        try {
          const cloudSummary = await getMySummary();
          if (cloudSummary) {
            const localSummary = summaryResponse.success && summaryResponse.data
              ? summaryResponse.data as TodaySummaryResponse
              : null;
            if (!localSummary) {
              setTodaySummary(cloudSummary as unknown as TodaySummaryResponse);
            }
          }
        } catch {
          // Cloud unavailable — local IPC data already set above
        }
      }

      // Fetch sync state
      const syncResponse = await sendQuery('getSyncState');
      if (syncResponse.success && syncResponse.data) {
        setSyncState(syncResponse.data as SyncStateResponse);
      }
    } catch (error) {
      console.error('[Dashboard] Error fetching data:', error);
    } finally {
      isFetchingRef.current = false;
    }
  }, [desktopRuntime, isConnected, currentUser?.id, sendQuery, setTrackingState, setTodaySummary, setSyncState]);

  // ============================================================================
  // TRACKING ACTIONS
  // ============================================================================

  const handlePauseTracking = useCallback(async () => {
    if (!desktopRuntime || !isConnected) return;
    try {
      const response = await sendQuery('getTrackingState');
      const currentState = response.data as TrackingStateResponse | undefined;
      const paused = currentState?.isPaused ?? false;

      // Toggle pause state
      const command = paused ? 'resumeTracking' : 'pauseTracking';
      const result = await sendCommand(command as 'pauseTracking' | 'resumeTracking');

      if (result.success) {
        setPaused(!paused);
      }
    } catch (error) {
      console.error('[Dashboard] Error toggling pause:', error);
    }
  }, [desktopRuntime, isConnected, sendQuery, sendCommand, setPaused]);

  const handleStopTracking = useCallback(async () => {
    if (!desktopRuntime || !isConnected) return;
    try {
      await sendCommand('stopTracking');
    } catch (error) {
      console.error('[Dashboard] Error stopping tracking:', error);
    }
  }, [desktopRuntime, isConnected, sendCommand]);

  const handleSyncNow = useCallback(async () => {
    if (!desktopRuntime || !isConnected) return;
    try {
      await sendCommand('syncNow');
    } catch (error) {
      console.error('[Dashboard] Error triggering sync:', error);
    }
  }, [desktopRuntime, isConnected, sendCommand]);

  // ============================================================================
  // LIFECYCLE - Initial fetch
  // ============================================================================

  useEffect(() => {
    if (desktopRuntime) {
      if (isConnected) fetchDashboardData();
      return;
    }
    fetchDashboardData();
  }, [desktopRuntime, isConnected, fetchDashboardData]);

  // ============================================================================
  // LIFECYCLE - Polling
  // ============================================================================

  useEffect(() => {
    if (desktopRuntime && !isConnected) return;

    const intervalMs = desktopRuntime ? POLLING_INTERVAL_MS : WEB_POLLING_INTERVAL_MS;
    pollingIntervalRef.current = setInterval(() => {
      // Avoid backend polling when tab is hidden (WebUI).
      if (!desktopRuntime && document.visibilityState !== 'visible') return;
      fetchDashboardData();
    }, intervalMs);

    return () => {
      if (pollingIntervalRef.current) {
        clearInterval(pollingIntervalRef.current);
      }
    };
  }, [desktopRuntime, isConnected, fetchDashboardData]);

  // ============================================================================
  // LIFECYCLE - Event subscriptions
  // ============================================================================

  // Tracking started
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('trackingStarted', () => {
      console.log('[Dashboard] Tracking started event received');
      fetchDashboardData();
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent, fetchDashboardData]);

  // Tracking stopped
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('trackingStopped', () => {
      console.log('[Dashboard] Tracking stopped event received');
      fetchDashboardData();
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent, fetchDashboardData]);

  // Tracking state changed
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('trackingStateChanged', (payload) => {
      console.log('[Dashboard] Tracking state changed:', payload);
      handleTrackingStateChanged(payload);
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent]);

  // Session updated
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('sessionUpdated', (payload) => {
      console.log('[Dashboard] Session updated:', payload);
      handleSessionUpdated(payload);

      // Also refresh summary data
      sendQuery('getTodaySummary').then((response) => {
        if (response.success && response.data) {
          setTodaySummary(response.data as TodaySummaryResponse);
        }
      });
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent, sendQuery, setTodaySummary]);

  // Sync progress changed
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('syncProgressChanged', (payload) => {
      console.log('[Dashboard] Sync progress:', payload);
      // Update sync state with progress info
      const currentSync = syncState;
      if (currentSync) {
        setSyncState({
          ...currentSync,
          status: payload.status as SyncStateResponse['status'],
        });
      }
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent, syncState, setSyncState]);

  // Sync completed
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('syncCompleted', () => {
      console.log('[Dashboard] Sync completed');
      // Refresh all dashboard data — newly synced sessions from other devices
      // should appear in the local dashboard immediately after sync.
      fetchDashboardData();
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent, sendQuery, setSyncState]);

  // Agent health changed
  useEffect(() => {
    if (!desktopRuntime || !isConnected) return;

    const unsubscribe = subscribeToEvent('agentHealthChanged', (payload) => {
      console.log('[Dashboard] Agent health changed:', payload);
      // Could update UI indicators based on health status
    });

    return unsubscribe;
  }, [desktopRuntime, isConnected, subscribeToEvent]);

  // Refresh when window becomes visible (restored from tray)
  useEffect(() => {
    const handleAppVisible = () => {
      console.log('[Dashboard] App became visible, refreshing data');
      fetchDashboardData();
    };
    window.addEventListener('app-visible', handleAppVisible);
    return () => window.removeEventListener('app-visible', handleAppVisible);
  }, [fetchDashboardData]);

  // ============================================================================
  // RETURN
  // ============================================================================

  return {
    // Data
    todaySummary,
    weeklyHistory,
    categories,
    syncState,
    isPaused,
    isTracking,

    // Actions
    handlePauseTracking,
    handleStopTracking,
    handleSyncNow,
    refreshData: fetchDashboardData,
  };
}
