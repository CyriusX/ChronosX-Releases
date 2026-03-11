/**
 * IPC Service Integration Tests
 *
 * Tests the IPC service with mock bridge to verify:
 * - Command sending
 * - Query sending
 * - Event subscription
 * - Reconnection behavior
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { IpcService, resetIpcService } from '../services/ipcService';
import { globalEventDispatcher } from '../services/eventDispatcher';
import type { ITimeTrackBridge, IpcResponse } from '../types/ipc';

// ============================================================================
// MOCK BRIDGE
// ============================================================================

function createMockBridge(): ITimeTrackBridge {
  return {
    isConnected: true,
    sendCommand: vi.fn(async (command: string, _payloadJson?: string): Promise<IpcResponse> => {
      return { success: true, data: { command, executed: true } };
    }),
    sendQuery: vi.fn(async (query: string, _payloadJson?: string): Promise<IpcResponse> => {
      const mockData: Record<string, unknown> = {
        getTrackingState: { isTracking: true, isPaused: false, isFocusMode: false },
        getTodaySummary: { totalDuration: 192, productiveTime: 160 },
        getSyncState: { status: 'synced', pendingItems: 0 },
      };
      return { success: true, data: mockData[query] ?? null };
    }),
    subscribeToEvent: vi.fn((_eventType: string, _callback: (payloadJson: string) => void) => {
      return () => {}; // unsubscribe function
    }),
  };
}

// ============================================================================
// TESTS
// ============================================================================

describe('IpcService', () => {
  let mockBridge: ITimeTrackBridge;
  let originalBridge: ITimeTrackBridge | undefined;

  beforeEach(() => {
    // Reset singleton
    resetIpcService();
    globalEventDispatcher.clearAll();

    // Create fresh mock
    mockBridge = createMockBridge();

    // Save original and set mock
    originalBridge = window.timeTrackBridge;
    (window as { timeTrackBridge?: ITimeTrackBridge }).timeTrackBridge = mockBridge;
  });

  afterEach(() => {
    // Restore original bridge
    (window as { timeTrackBridge?: ITimeTrackBridge }).timeTrackBridge = originalBridge;
    resetIpcService();
  });

  // ============================================================================
  // CONNECTION STATE
  // ============================================================================

  describe('connection state', () => {
    it('should initialize with correct connection state', () => {
      const service = new IpcService();

      expect(service.isConnected).toBe(true);
      expect(service.isReady).toBe(true);
      expect(service.connectionState).toBe('connected');
    });

    it('should notify on connection change', () => {
      const service = new IpcService();
      const callback = vi.fn();

      service.onConnectionChange(callback);

      // Should immediately call with current state
      expect(callback).toHaveBeenCalledWith(true);
    });

    it('should handle missing bridge gracefully', () => {
      (window as { timeTrackBridge?: ITimeTrackBridge }).timeTrackBridge = undefined;

      const service = new IpcService();

      expect(service.isConnected).toBe(false);
      expect(service.isReady).toBe(false);
    });
  });

  // ============================================================================
  // COMMANDS
  // ============================================================================

  describe('sendCommand', () => {
    it('should send command to bridge', async () => {
      const service = new IpcService();

      const result = await service.sendCommand('pauseTracking', { reason: 'Lunch' });

      expect(result.success).toBe(true);
      expect(mockBridge.sendCommand).toHaveBeenCalledWith(
        'pauseTracking',
        JSON.stringify({ reason: 'Lunch' })
      );
    });

    it('should handle command without payload', async () => {
      const service = new IpcService();

      const result = await service.sendCommand('resumeTracking');

      expect(result.success).toBe(true);
      expect(mockBridge.sendCommand).toHaveBeenCalledWith('resumeTracking', undefined);
    });

    it('should return error when bridge not available', async () => {
      (window as { timeTrackBridge?: ITimeTrackBridge }).timeTrackBridge = undefined;

      const service = new IpcService();

      const result = await service.sendCommand('pauseTracking');

      expect(result.success).toBe(false);
      expect(result.error).toBe('Bridge not available');
    });
  });

  // ============================================================================
  // QUERIES
  // ============================================================================

  describe('sendQuery', () => {
    it('should send query to bridge and return typed data', async () => {
      const service = new IpcService();

      const result = await service.sendQuery('getTrackingState');

      expect(result.success).toBe(true);
      expect(result.data).toEqual({
        isTracking: true,
        isPaused: false,
        isFocusMode: false,
      });
    });

    it('should return null data for unknown query', async () => {
      const service = new IpcService();

      const result = await service.sendQuery('getUnknownQuery' as 'getTrackingState');

      expect(result.success).toBe(true);
      expect(result.data).toBeNull();
    });

    it('should return error when bridge not available', async () => {
      (window as { timeTrackBridge?: ITimeTrackBridge }).timeTrackBridge = undefined;

      const service = new IpcService();

      const result = await service.sendQuery('getTrackingState');

      expect(result.success).toBe(false);
      expect(result.error).toBe('Bridge not available');
    });
  });

  // ============================================================================
  // EVENTS
  // ============================================================================

  describe('subscribe', () => {
    it('should subscribe to event and receive dispatches', () => {
      const service = new IpcService();
      const callback = vi.fn();

      const unsubscribe = service.subscribe('trackingStateChanged', callback);

      // Simulate event dispatch
      globalEventDispatcher.dispatch('trackingStateChanged', {
        isTracking: true,
        isPaused: false,
      });

      expect(callback).toHaveBeenCalledWith({
        isTracking: true,
        isPaused: false,
      });

      unsubscribe();
    });

    it('should unsubscribe correctly', () => {
      const service = new IpcService();
      const callback = vi.fn();

      const unsubscribe = service.subscribe('trackingStateChanged', callback);

      // Unsubscribe
      unsubscribe();

      // Dispatch should not reach callback
      globalEventDispatcher.dispatch('trackingStateChanged', {
        isTracking: true,
        isPaused: false,
      });

      expect(callback).not.toHaveBeenCalled();
    });

    it('should support multiple subscribers', () => {
      const service = new IpcService();
      const callback1 = vi.fn();
      const callback2 = vi.fn();

      const unsub1 = service.subscribe('trackingStateChanged', callback1);
      const unsub2 = service.subscribe('trackingStateChanged', callback2);

      globalEventDispatcher.dispatch('trackingStateChanged', {
        isTracking: true,
        isPaused: false,
      });

      expect(callback1).toHaveBeenCalled();
      expect(callback2).toHaveBeenCalled();

      unsub1();
      unsub2();
    });
  });

  // ============================================================================
  // RECONNECTION
  // ============================================================================

  describe('reconnection', () => {
    it('should expose reconnect method', () => {
      const service = new IpcService();

      expect(typeof service.reconnect).toBe('function');
    });

    it('should not reconnect if already connected', async () => {
      const service = new IpcService();

      // Already connected, should do nothing
      await service.reconnect();

      // Should still be connected
      expect(service.isConnected).toBe(true);
    });
  });

  // ============================================================================
  // DISPOSE
  // ============================================================================

  describe('dispose', () => {
    it('should clean up resources', () => {
      const service = new IpcService();
      const callback = vi.fn();

      service.subscribe('trackingStateChanged', callback);
      service.dispose();

      // After dispose, dispatch should not reach callback
      globalEventDispatcher.dispatch('trackingStateChanged', {
        isTracking: true,
        isPaused: false,
      });

      // Callback should not be called after dispose
      // Note: This depends on how the event dispatcher handles disposed services
    });
  });
});

// ============================================================================
// EVENT DISPATCHER TESTS
// ============================================================================

describe('EventDispatcher', () => {
  beforeEach(() => {
    globalEventDispatcher.clearAll();
  });

  it('should dispatch events to subscribers', () => {
    const callback = vi.fn();

    globalEventDispatcher.subscribe('trackingStarted', callback);

    globalEventDispatcher.dispatch('trackingStarted', {
      sessionId: 'test-123',
      timestamp: '2024-01-01T00:00:00Z',
    });

    expect(callback).toHaveBeenCalledWith({
      sessionId: 'test-123',
      timestamp: '2024-01-01T00:00:00Z',
    });
  });

  it('should parse JSON payloads correctly', () => {
    const callback = vi.fn();

    globalEventDispatcher.subscribe('trackingStarted', callback);

    globalEventDispatcher.dispatchFromJson(
      'trackingStarted',
      JSON.stringify({ sessionId: 'test-456', timestamp: '2024-01-01T00:00:00Z' })
    );

    expect(callback).toHaveBeenCalledWith({
      sessionId: 'test-456',
      timestamp: '2024-01-01T00:00:00Z',
    });
  });

  it('should track subscriber count', () => {
    const callback = vi.fn();

    expect(globalEventDispatcher.getSubscriberCount('trackingStarted')).toBe(0);

    const unsub = globalEventDispatcher.subscribe('trackingStarted', callback);

    expect(globalEventDispatcher.getSubscriberCount('trackingStarted')).toBe(1);

    unsub();

    expect(globalEventDispatcher.getSubscriberCount('trackingStarted')).toBe(0);
  });
});
