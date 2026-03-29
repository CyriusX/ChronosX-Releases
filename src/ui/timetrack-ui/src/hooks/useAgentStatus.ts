/**
 * useAgentStatus Hook - Gerenciamento de state do Agent em tempo real
 *
 * SOLID:
 * - SRP: Apenas gerencia state do agent para UI
 * - DIP: Depende da useIpc abstraction
 *
 * Features:
 * - Real-time updates via IPC push events (sem polling)
 * - Health level computation automática
 * - Detecta quando AgentService está offline (queries falham)
 * - Zero ações do usuário - apenas leitura
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useIpc } from './useIpc';
import type {
  CurrentStatusResponse,
  SyncStateResponse,
  ErrorItem as IpcErrorItem,
} from '../types/ipc';
import {
  type AgentStatusUI,
  computeAgentHealth,
  formatRelativeTime,
  HEALTH_CONFIG,
} from '../types/agentStatus';

// ============================================================================
// INITIAL STATE
// ============================================================================

const initialStatus: AgentStatusUI = {
  health: 'unhealthy',
  state: 'stopped',
  uptime: 0,
  version: '-',
  syncStatus: 'pending',
  pendingItems: 0,
  failedItems: 0,
  recentErrors: [],
  healthIndicator: HEALTH_CONFIG.unhealthy,
};

// ============================================================================
// HOOK
// ============================================================================

export function useAgentStatus() {
  const { sendQuery, subscribeToEvent } = useIpc();
  const [status, setStatus] = useState<AgentStatusUI>(initialStatus);
  const [isLoading, setIsLoading] = useState(true);
  const [isAgentOnline, setIsAgentOnline] = useState(false);
  const consecutiveFailuresRef = useRef(0);

  // Fetch all status data
  const fetchAgentStatus = useCallback(async () => {
    try {
      // Query current status (agent state, uptime, version)
      const statusResponse = await sendQuery('getCurrentStatus');
      // Query sync state (last sync, pending items)
      const syncResponse = await sendQuery('getSyncState');
      // Query recent errors
      const errorsResponse = await sendQuery('getErrors');

      // Check if queries failed - AgentService might be offline
      if (!statusResponse.success) {
        console.warn('[useAgentStatus] getCurrentStatus failed:', statusResponse.error);
        handleAgentOffline();
        return;
      }

      if (!syncResponse.success) {
        console.warn('[useAgentStatus] getSyncState failed:', syncResponse.error);
        handleAgentOffline();
        return;
      }

      // Reset failure counter on success
      consecutiveFailuresRef.current = 0;
      setIsAgentOnline(true);

      const currentStatus = statusResponse.data as CurrentStatusResponse;
      const syncState = syncResponse.data as SyncStateResponse;
      const errors = errorsResponse.success
        ? (errorsResponse.data as { errors: IpcErrorItem[] })
        : null;

      const recentErrors = errors?.errors?.filter(e => !e.resolved).slice(0, 5) ?? [];

      // Compute health level
      const health = computeAgentHealth(
        syncState.status,
        syncState.pendingItems,
        syncState.failedItems,
        recentErrors ?? []
      );

      setStatus({
        health,
        state: currentStatus.state,
        uptime: currentStatus.uptime,
        version: currentStatus.version,
        syncStatus: syncState.status,
        lastSyncAt: syncState.lastSyncAt,
        pendingItems: syncState.pendingItems,
        failedItems: syncState.failedItems,
        recentErrors: recentErrors ?? [],
        healthIndicator: HEALTH_CONFIG[health],
        lastSyncRelative: formatRelativeTime(syncState.lastSyncAt),
      });
    } catch (error) {
      console.error('[useAgentStatus] Error fetching status:', error);
      handleAgentOffline();
    } finally {
      setIsLoading(false);
    }
  }, [sendQuery]);

  // Handle when AgentService is detected as offline
  const handleAgentOffline = useCallback(() => {
    consecutiveFailuresRef.current++;

    // After 2 consecutive failures, mark as offline
    if (consecutiveFailuresRef.current >= 2) {
      console.warn('[useAgentStatus] AgentService detected as offline after consecutive failures');
      setIsAgentOnline(false);
      setStatus(prev => ({
        ...prev,
        health: 'unhealthy',
        state: 'stopped',
        healthIndicator: HEALTH_CONFIG.unhealthy,
        syncStatus: 'failed',
      }));
    }
  }, []);

  // ============================================================================
  // LIFECYCLE - Initial fetch and periodic refresh
  // ============================================================================

  useEffect(() => {
    // Fetch immediately
    fetchAgentStatus();

    // Set up periodic refresh every 10 seconds to detect offline state
    const interval = setInterval(() => {
      fetchAgentStatus();
    }, 10000);

    return () => {
      clearInterval(interval);
    };
  }, [fetchAgentStatus]);

  // ============================================================================
  // LIFECYCLE - Event subscriptions (real-time updates)
  // ============================================================================

  // Agent health changed - update immediately
  useEffect(() => {
    const unsubscribe = subscribeToEvent('agentHealthChanged', (payload) => {
      console.log('[useAgentStatus] agentHealthChanged event:', payload);

      // If we receive this event, agent is online
      consecutiveFailuresRef.current = 0;
      setIsAgentOnline(true);

      // Refetch all data to get complete picture
      fetchAgentStatus();
    });

    return unsubscribe;
  }, [subscribeToEvent, fetchAgentStatus]);

  // Sync progress changed - update sync status
  useEffect(() => {
    const unsubscribe = subscribeToEvent('syncProgressChanged', (payload) => {
      // Reset failures on sync activity
      consecutiveFailuresRef.current = 0;
      setIsAgentOnline(true);

      // Map payload status to AgentStatusUI syncStatus
      const statusMap: Record<string, AgentStatusUI['syncStatus']> = {
        pending: 'pending',
        in_progress: 'syncing',
        completed: 'synced',
        failed: 'failed',
      };

      setStatus(prev => ({
        ...prev,
        syncStatus: statusMap[payload.status] || 'pending',
        pendingItems: payload.progress < 100 ? prev.pendingItems : 0,
        lastSyncRelative: undefined,
      }));
    });

    return unsubscribe;
  }, [subscribeToEvent]);

  // Sync completed - refresh sync state
  useEffect(() => {
    const unsubscribe = subscribeToEvent('syncCompleted', () => {
      consecutiveFailuresRef.current = 0;
      setIsAgentOnline(true);
      fetchAgentStatus();
    });

    return unsubscribe;
  }, [subscribeToEvent, fetchAgentStatus]);

  // Connection state changed
  useEffect(() => {
    const unsubscribe = subscribeToEvent('connectionStateChanged', (payload) => {
      console.log('[useAgentStatus] connectionStateChanged event:', payload);

      if (!payload.isConnected) {
        setIsAgentOnline(false);
        setStatus(prev => ({
          ...prev,
          health: 'unhealthy',
          healthIndicator: HEALTH_CONFIG.unhealthy,
        }));
      } else {
        setIsAgentOnline(true);
        fetchAgentStatus();
      }
    });

    return unsubscribe;
  }, [subscribeToEvent, fetchAgentStatus]);

  return {
    status,
    isLoading,
    isAgentOnline,
    refresh: fetchAgentStatus,
  };
}
