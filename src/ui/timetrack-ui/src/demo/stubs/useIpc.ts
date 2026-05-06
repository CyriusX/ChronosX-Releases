import { useCallback } from 'react';
import type { CommandPayloadMap, EventPayloadMap, IpcResponse, QueryResponseMap } from '../../types/ipc';
import {
  demoTodaySummary,
  demoProjectsForIpc,
  demoFocusActivities,
} from '../demoData';

type ConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

type UseIpcReturn = {
  isConnected: boolean;
  isReady: boolean;
  connectionState: ConnectionState;
  sendCommand: <K extends keyof CommandPayloadMap>(command: K, payload?: CommandPayloadMap[K]) => Promise<IpcResponse<void>>;
  sendQuery: <K extends keyof QueryResponseMap>(query: K, payload?: Record<string, unknown>) => Promise<IpcResponse<QueryResponseMap[K]>>;
  subscribeToEvent: <K extends keyof EventPayloadMap>(eventType: K, callback: (payload: EventPayloadMap[K]) => void) => () => void;
  reconnect: () => Promise<void>;
};

const demoState = {
  isTracking: true,
  isPaused: false,
  isFocusMode: false,
};

function ok<T>(data: T): IpcResponse<T> {
  return { success: true, data };
}

export function useIpc(): UseIpcReturn {
  const sendCommand = useCallback(async <K extends keyof CommandPayloadMap>(
    command: K,
    _payload?: CommandPayloadMap[K],
  ): Promise<IpcResponse<void>> => {
    if (command === 'startTracking') {
      demoState.isTracking = true;
      demoState.isPaused = false;
    }
    if (command === 'stopTracking') {
      demoState.isTracking = false;
      demoState.isPaused = false;
    }
    if (command === 'pauseTracking') {
      demoState.isTracking = false;
      demoState.isPaused = true;
    }
    if (command === 'resumeTracking') {
      demoState.isTracking = true;
      demoState.isPaused = false;
    }
    return ok(undefined);
  }, []);

  const sendQuery = useCallback(async <K extends keyof QueryResponseMap>(
    query: K,
    _payload?: Record<string, unknown>,
  ): Promise<IpcResponse<QueryResponseMap[K]>> => {
    switch (query) {
      case 'getTodaySummary':
        return ok(demoTodaySummary as unknown as QueryResponseMap[K]);
      case 'getTrackingState':
        return ok({ ...demoState } as unknown as QueryResponseMap[K]);
      case 'getSyncState':
        return ok({
          status: 'synced',
          lastSyncAt: new Date(Date.now() - 4 * 60_000).toISOString(),
          pendingItems: 0,
          failedItems: 0,
        } as unknown as QueryResponseMap[K]);
      case 'getCurrentStatus':
        return ok({
          state: 'running',
          uptime: 6 * 3600,
          version: 'demo',
          desktopHostVersion: 'demo',
        } as unknown as QueryResponseMap[K]);
      case 'getErrors':
        return ok({ errors: [], total: 0 } as unknown as QueryResponseMap[K]);
      case 'getSettings':
        return ok({
          autoResumeNotificationEnabled: true,
          notificationSoundsEnabled: false,
          language: 'en-US',
          idleThresholdSeconds: 180,
          workGoalSeconds: 28800,
          updatedAt: new Date().toISOString(),
        } as unknown as QueryResponseMap[K]);
      case 'getProjects':
        return ok(demoProjectsForIpc as unknown as QueryResponseMap[K]);
      case 'getRecentActivities':
        return ok({ activities: demoFocusActivities, total: demoFocusActivities.length } as unknown as QueryResponseMap[K]);
      default:
        return ok({} as unknown as QueryResponseMap[K]);
    }
  }, []);

  const subscribeToEvent = useCallback(<K extends keyof EventPayloadMap>(
    _eventType: K,
    _callback: (payload: EventPayloadMap[K]) => void,
  ) => {
    return () => {};
  }, []);

  const reconnect = useCallback(async () => {}, []);

  return {
    isConnected: true,
    isReady: true,
    connectionState: 'connected',
    sendCommand,
    sendQuery,
    subscribeToEvent,
    reconnect,
  };
}
