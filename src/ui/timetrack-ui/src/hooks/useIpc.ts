/**
 * useIpc Hook - React integration for IPC Service
 *
 * SOLID:
 * - DIP: Depends on IIpcClient abstraction via getIpcService()
 * - SRP: Only bridges React lifecycle to IPC service
 *
 * This hook provides a React-friendly interface to the IPC service,
 * handling lifecycle and providing stable callback references.
 */

import { useCallback, useEffect, useState, useRef } from 'react';
import { getIpcService } from '../services';
import type {
  IIpcClient,
  IpcResponse,
  CommandPayloadMap,
  QueryResponseMap,
  EventPayloadMap,
  FocusModeSnapshot,
} from '../types/ipc';

// ============================================================================
// TYPES
// ============================================================================

interface UseIpcReturn {
  // Connection state
  isConnected: boolean;
  isReady: boolean;
  connectionState: 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

  // Commands (fire-and-forget)
  sendCommand: <K extends keyof CommandPayloadMap>(
    command: K,
    payload?: CommandPayloadMap[K]
  ) => Promise<IpcResponse<void>>;

  // Queries (request-response)
  sendQuery: <K extends keyof QueryResponseMap>(
    query: K,
    payload?: Record<string, unknown>
  ) => Promise<IpcResponse<QueryResponseMap[K]>>;

  // Events (subscription)
  subscribeToEvent: <K extends keyof EventPayloadMap>(
    eventType: K,
    callback: (payload: EventPayloadMap[K]) => void
  ) => () => void;

  // Connection management
  reconnect: () => Promise<void>;
}

// ============================================================================
// MOCK DATA (for development without bridge)
// ============================================================================

const mockTrackingState = {
  isTracking: true,
  isPaused: false,
  isFocusMode: false,
};

const mockTodaySummary = {
  totalDuration: 11520, // 3h 12m in seconds (192 * 60)
  productiveTime: 9600, // 160m in seconds
  idleTime: 1920,       // 32m in seconds
  focusTime: 4920,      // 82m in seconds
  sessionsCount: 8,
  topProjects: [
    { name: 'A Gente (App)', duration: 9960, percentage: 49 },
    { name: 'Cliente X', duration: 4500, percentage: 30 },
    { name: 'Estudos', duration: 1200, percentage: 12 },
    { name: 'Admin', duration: 900, percentage: 9 },
  ],
  topApplications: [
    { name: 'VS Code', duration: 9960, percentage: 59 },
    { name: 'Chrome', duration: 6180, percentage: 16 },
    { name: 'Slack', duration: 1200, percentage: 8 },
    { name: 'Spotify', duration: 1200, percentage: 5 },
  ],
  categories: [
    { name: 'Desenvolvimento', duration: 24120, percentage: 64, color: '#05df72' },
    { name: 'Reuniões', duration: 4500, percentage: 17, color: '#4ad9ff' },
    { name: 'Pesquisa', duration: 2760, percentage: 10, color: '#8b7aff' },
    { name: 'Comunicação', duration: 2340, percentage: 9, color: '#ff9c5b' },
  ],
  weeklyHistory: [
    { date: '2024-01-08', dayName: 'Seg', hours: 6.5, isToday: false },
    { date: '2024-01-09', dayName: 'Ter', hours: 7.2, isToday: false },
    { date: '2024-01-10', dayName: 'Qua', hours: 8.0, isToday: false },
    { date: '2024-01-11', dayName: 'Qui', hours: 5.8, isToday: false },
    { date: '2024-01-12', dayName: 'Sex', hours: 7.5, isToday: false },
    { date: '2024-01-13', dayName: 'Sáb', hours: 2.0, isToday: false },
    { date: '2024-01-14', dayName: 'Dom', hours: 3.2, isToday: true },
  ],
};

const mockCurrentStatus = {
  state: 'running' as const,
  uptime: 3600,
  version: '1.0.0',
  sessionId: 'session-123',
};

const mockSyncState = {
  status: 'synced' as const,
  lastSyncAt: new Date().toISOString(),
  pendingItems: 0,
  failedItems: 0,
};

// CX-139: Mock Focus Mode state for development (mutable)
let mockFocusModeState: FocusModeSnapshot = {
  state: 'Off',
  mode: 'Pomodoro',
  remainingMs: 0,
  cycleNumber: 0,
  totalCyclesToday: 0,
  nextBreakType: 'Short',
  plannedDurationMs: 25 * 60 * 1000, // 25 minutes
  allowUserOverride: true,
  timestamp: new Date().toISOString(),
};

// ============================================================================
// MOCK IPC CLIENT (for development)
// ============================================================================

class MockIpcClient implements IIpcClient {
  readonly isConnected = true;
  readonly isReady = true;
  readonly connectionState = 'connected' as const;

  async sendCommand<K extends keyof CommandPayloadMap>(
    command: K,
    _payload?: CommandPayloadMap[K]
  ): Promise<IpcResponse<void>> {
    await this.delay(50);
    console.log('[MockIPC] Command executed:', command);

    // Simulate Focus Mode state changes for development
    if (command === 'startFocusMode') {
      mockFocusModeState = {
        ...mockFocusModeState,
        state: 'FocusRunning',
        remainingMs: 25 * 60 * 1000, // 25 minutes
        cycleNumber: mockFocusModeState.cycleNumber + 1,
        timestamp: new Date().toISOString(),
      };
      console.log('[MockIPC] Focus mode started, state:', mockFocusModeState.state);
    } else if (command === 'stopFocusMode') {
      mockFocusModeState = {
        ...mockFocusModeState,
        state: 'Off',
        remainingMs: 0,
        timestamp: new Date().toISOString(),
      };
      console.log('[MockIPC] Focus mode stopped');
    } else if (command === 'pauseFocusMode') {
      mockFocusModeState = {
        ...mockFocusModeState,
        state: 'FocusPaused',
        timestamp: new Date().toISOString(),
      };
      console.log('[MockIPC] Focus mode paused');
    } else if (command === 'resumeFocusMode') {
      mockFocusModeState = {
        ...mockFocusModeState,
        state: 'FocusRunning',
        timestamp: new Date().toISOString(),
      };
      console.log('[MockIPC] Focus mode resumed');
    } else if (command === 'skipBreak') {
      mockFocusModeState = {
        ...mockFocusModeState,
        state: 'FocusRunning',
        remainingMs: 25 * 60 * 1000,
        cycleNumber: mockFocusModeState.cycleNumber + 1,
        timestamp: new Date().toISOString(),
      };
      console.log('[MockIPC] Break skipped, starting new focus cycle');
    }

    return { success: true };
  }

  async sendQuery<K extends keyof QueryResponseMap>(
    query: K,
    _payloadJson?: string
  ): Promise<IpcResponse<QueryResponseMap[K]>> {
    await this.delay(50);

    // Return mock data based on query type
    const mockData: Record<string, unknown> = {
      getTrackingState: mockTrackingState,
      getTodaySummary: mockTodaySummary,
      getCurrentStatus: mockCurrentStatus,
      getSyncState: mockSyncState,
      getFocusModeState: { ...mockFocusModeState, timestamp: new Date().toISOString() },
    };

    const data = mockData[query as string] ?? null;
    console.log('[MockIPC] Query executed:', query, data);

    return { success: true, data: data as QueryResponseMap[K] };
  }

  subscribe<K extends keyof EventPayloadMap>(
    _eventType: K,
    _callback: (payload: EventPayloadMap[K]) => void
  ): () => void {
    // Return no-op unsubscribe for mock
    return () => {};
  }

  onConnectionChange(callback: (isConnected: boolean) => void): () => void {
    callback(true);
    return () => {};
  }

  async reconnect(): Promise<void> {
    // No-op for mock
  }

  private delay(ms: number): Promise<void> {
    return new Promise((resolve) => setTimeout(resolve, ms));
  }
}

// ============================================================================
// FACTORY FUNCTION
// ============================================================================

function createIpcClient(): IIpcClient {
  // Check if running in browser with bridge available
  console.log('[useIpc] Checking for bridge...');
  console.log('[useIpc] window.timeTrackBridge:', window.timeTrackBridge);
  const chrome = (window as unknown as Record<string, Record<string, unknown>>).chrome;
  console.log('[useIpc] chrome.webview:', chrome?.webview);

  if (typeof window !== 'undefined' && window.timeTrackBridge) {
    console.log('[useIpc] Bridge found! Using real IpcService');
    return getIpcService();
  }

  // Return mock client for development
  console.log('[useIpc] Bridge not available, using mock client');
  return new MockIpcClient();
}

// ============================================================================
// HOOK
// ============================================================================

/**
 * Hook for IPC communication with AgentService via WebView2 Bridge
 *
 * Automatically handles:
 * - Connection state tracking
 * - Event subscriptions with cleanup
 * - Mock data in development mode
 */
export function useIpc(): UseIpcReturn {
  const [isConnected, setIsConnected] = useState(false);
  const [isReady, setIsReady] = useState(false);
  const [connectionState, setConnectionState] = useState<
    'disconnected' | 'connecting' | 'connected' | 'reconnecting'
  >('disconnected');

  const clientRef = useRef<IIpcClient | null>(null);

  // Initialize client on mount
  useEffect(() => {
    const client = createIpcClient();
    clientRef.current = client;

    setIsReady(client.isReady);
    setIsConnected(client.isConnected);
    setConnectionState(client.connectionState);

    // Subscribe to connection changes
    const unsubscribe = client.onConnectionChange((connected) => {
      setIsConnected(connected);
      setConnectionState(connected ? 'connected' : 'disconnected');
    });

    return () => {
      unsubscribe();
    };
  }, []);

  // Command sender with stable reference
  const sendCommand = useCallback(
    async <K extends keyof CommandPayloadMap>(
      command: K,
      payload?: CommandPayloadMap[K]
    ): Promise<IpcResponse<void>> => {
      if (!clientRef.current) {
        return { success: false, error: 'IPC client not initialized' };
      }
      return clientRef.current.sendCommand(command, payload);
    },
    []
  );

  // Query sender with stable reference
  const sendQuery = useCallback(
    async <K extends keyof QueryResponseMap>(
      query: K,
      payload?: Record<string, unknown>
    ): Promise<IpcResponse<QueryResponseMap[K]>> => {
      if (!clientRef.current) {
        return { success: false, error: 'IPC client not initialized' };
      }
      const payloadJson = payload ? JSON.stringify(payload) : undefined;
      return clientRef.current.sendQuery(query, payloadJson);
    },
    []
  );

  // Event subscriber with stable reference
  const subscribeToEvent = useCallback(
    <K extends keyof EventPayloadMap>(
      eventType: K,
      callback: (payload: EventPayloadMap[K]) => void
    ): (() => void) => {
      if (!clientRef.current) {
        return () => {};
      }
      return clientRef.current.subscribe(eventType as K, callback);
    },
    []
  );

  // Reconnect function
  const reconnect = useCallback(async (): Promise<void> => {
    if (!clientRef.current) return;
    setConnectionState('reconnecting');
    await clientRef.current.reconnect();
  }, []);

  return {
    isConnected,
    isReady,
    connectionState,
    sendCommand,
    sendQuery,
    subscribeToEvent,
    reconnect,
  };
}

// ============================================================================
// LEGACY COMPATIBILITY
// ============================================================================

/**
 * @deprecated Use useIpc() instead
 */
export function useIpcConnection() {
  const { isConnected, isReady, connectionState, reconnect } = useIpc();
  return { isConnected, isReady, connectionState, reconnect };
}
