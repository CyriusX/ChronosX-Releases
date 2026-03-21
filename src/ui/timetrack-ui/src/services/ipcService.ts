/**
 * IPC Service - Simplified for WebView2 bridge communication
 *
 * Features:
 * - Type-safe commands and queries
 * - Connection state tracking via bridge existence
 * - NO polling - relies on bridge events
 */

import type {
  IIpcClient,
  ITimeTrackBridge,
  IpcResponse,
  CommandPayloadMap,
  QueryResponseMap,
  EventPayloadMap,
  ConnectionState,
} from '../types/ipc';
import type { IReconnectionStrategy } from './reconnectionStrategy';
import { createDefaultReconnectionStrategy } from './reconnectionStrategy';
import { EventDispatcher, globalEventDispatcher } from './eventDispatcher';

// ============================================================================
// TYPES
// ============================================================================

type ConnectionCallback = (isConnected: boolean) => void;

export interface IpcServiceConfig {
  reconnectionStrategy?: IReconnectionStrategy;
  eventDispatcher?: EventDispatcher;
  checkConnectionIntervalMs?: number;
}

const DEFAULT_CONFIG = {
  reconnectionStrategy: createDefaultReconnectionStrategy(),
  eventDispatcher: globalEventDispatcher,
  checkConnectionIntervalMs: 5000,
};

// ============================================================================
// IPC SERVICE (SIMPLIFIED - NO POLLING)
// ============================================================================

export class IpcService implements IIpcClient {
  private readonly config: typeof DEFAULT_CONFIG;
  private readonly connectionCallbacks: Set<ConnectionCallback> = new Set();
  private readonly pendingCommands: Map<string, {
    resolve: (response: IpcResponse) => void;
    reject: (error: Error) => void;
  }> = new Map();

  private _connectionState: ConnectionState = 'disconnected';
  private _isReady: boolean = false;
  private reconnectionTimeout: ReturnType<typeof setTimeout> | null = null;
  private connectionCheckInterval: ReturnType<typeof setInterval> | null = null;

  constructor(config: IpcServiceConfig = {}) {
    this.config = { ...DEFAULT_CONFIG, ...config };
    this.initialize();
  }

  // ============================================================================
  // IConnectionManager Implementation
  // ============================================================================

  get isConnected(): boolean {
    return this._connectionState === 'connected' || !!this.getBridge();
  }

  get isReady(): boolean {
    return this._isReady;
  }

  get connectionState(): ConnectionState {
    return this._connectionState;
  }

  onConnectionChange(callback: ConnectionCallback): () => void {
    this.connectionCallbacks.add(callback);
    callback(this.isConnected);
    return () => this.connectionCallbacks.delete(callback);
  }

  async reconnect(): Promise<void> {
    if (this._connectionState === 'connected') {
      return;
    }
    this.config.reconnectionStrategy.reset();
    await this.attemptReconnection();
  }

  // ============================================================================
  // ICommandSender Implementation
  // ============================================================================

  async sendCommand<K extends keyof CommandPayloadMap>(
    command: K,
    payload?: CommandPayloadMap[K]
  ): Promise<IpcResponse<void>> {
    const bridge = this.getBridge();

    if (!bridge) {
      return { success: false, error: 'Bridge not available' };
    }

    try {
      const payloadJson = payload !== undefined ? JSON.stringify(payload) : undefined;

      // Bridge is either a postMessage-based bridge (returns native Promises)
      // or a COM proxy. Both expose SendCommand returning Promise<string>.
      const responseJson = await (bridge as unknown as {
        SendCommand: (command: string, payloadJson?: string) => Promise<string>;
      }).SendCommand(command, payloadJson);

      const response = JSON.parse(responseJson);
      return response as IpcResponse<void>;
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      return { success: false, error: errorMessage };
    }
  }

  // ============================================================================
  // IQuerySender Implementation
  // ============================================================================

  async sendQuery<K extends keyof QueryResponseMap>(
    query: K,
    payloadJson?: string
  ): Promise<IpcResponse<QueryResponseMap[K]>> {
    const bridge = this.getBridge();

    if (!bridge) {
      console.warn(`[IpcService] Bridge not available for query: ${query}`);
      return { success: false, error: 'Bridge not available' };
    }

    try {
      // Bridge is either a postMessage-based bridge (returns native Promises)
      // or a COM proxy. Both expose SendQuery returning Promise<string>.
      const responseJson = await (bridge as unknown as {
        SendQuery: (query: string, payloadJson?: string) => Promise<string>;
      }).SendQuery(query as string, payloadJson);

      const response = JSON.parse(responseJson);
      return response as IpcResponse<QueryResponseMap[K]>;
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : String(error);
      console.error(`[IpcService] Error sending query ${query}:`, errorMessage);
      return { success: false, error: errorMessage };
    }
  }

  // ============================================================================
  // IEventSubscriber Implementation
  // ============================================================================

  subscribe<K extends keyof EventPayloadMap>(
    eventType: K,
    callback: (payload: EventPayloadMap[K]) => void
  ): () => void {
    const localUnsubscribe = this.config.eventDispatcher.subscribe(eventType, callback);
    this.setupBridgeEventSubscription(eventType);
    return () => {
      localUnsubscribe();
      if (!this.config.eventDispatcher.hasSubscribers(eventType)) {
        this.config.eventDispatcher.clearEventType(eventType);
      }
    };
  }

  // ============================================================================
  // PUBLIC METHODS
  // ============================================================================

  dispose(): void {
    this.clearReconnectionTimeout();
    this.clearConnectionCheckInterval();
    this.config.eventDispatcher.clearAll();
    this.pendingCommands.clear();
    this.connectionCallbacks.clear();
    this._connectionState = 'disconnected';
    this._isReady = false;
  }

  // ============================================================================
  // PRIVATE METHODS
  // ============================================================================

  private getBridge(): ITimeTrackBridge | undefined {
    if (typeof window === 'undefined') {
      return undefined;
    }
    return window.timeTrackBridge;
  }

  private initialize(): void {
    if (typeof window === 'undefined') {
      return;
    }

    const bridge = this.getBridge();
    const hasBridge = bridge !== undefined;

    console.log('[IpcService] Initializing, bridge found:', hasBridge);

    this._isReady = hasBridge;

    if (hasBridge) {
      this._connectionState = 'connected';
      this.setupBridgeHandlers();
      console.log('[IpcService] Bridge available, assuming connected (no polling)');
    } else {
      console.warn('[IpcService] Bridge not available, starting connection check interval');
      this._connectionState = 'disconnected';

      // Poll until the bridge becomes available (injected by WebView2 after document load)
      this.connectionCheckInterval = setInterval(() => {
        if (this.getBridge()) {
          this.clearConnectionCheckInterval();
          this._connectionState = 'connected';
          this._isReady = true;
          this.setupBridgeHandlers();
          console.log('[IpcService] Bridge became available, now connected');
          this.notifyConnectionChange();
        }
      }, this.config.checkConnectionIntervalMs);
    }

    this.notifyConnectionChange();
  }

  private setupBridgeHandlers(): void {
    console.log('[IpcService] Setting up bridge handlers - defining window.timeTrackHandleEvent');
    (window as unknown as Record<string, unknown>).timeTrackHandleEvent = (
      eventType: string,
      payloadJson: string
    ) => {
      console.log(`[IpcService] timeTrackHandleEvent called: eventType=${eventType}`);
      this.config.eventDispatcher.dispatchFromJson(eventType, payloadJson);
    };
  }

  private setupBridgeEventSubscription<K extends keyof EventPayloadMap>(eventType: K): void {
    // Skip bridge event subscription - WebViewBridge doesn't have subscribeToEvent method
    // Events are pushed from C# via window.timeTrackHandleEvent instead
    // This method is kept for future extensibility if bridge-based subscription is added
    console.log(`[IpcService] Event subscription for ${eventType} - using push mode (no bridge method)`);
  }

  private async attemptReconnection(): Promise<void> {
    const bridge = this.getBridge();
    if (bridge) {
      this._connectionState = 'connected';
      this.notifyConnectionChange();
    }
  }

  private notifyConnectionChange(): void {
    const isConnected = this.isConnected;
    this.connectionCallbacks.forEach((callback) => {
      try {
        callback(isConnected);
      } catch (error) {
        console.error('[IpcService] Error in connection callback:', error);
      }
    });
  }

  private clearReconnectionTimeout(): void {
    if (this.reconnectionTimeout) {
      clearTimeout(this.reconnectionTimeout);
      this.reconnectionTimeout = null;
    }
  }

  private clearConnectionCheckInterval(): void {
    if (this.connectionCheckInterval) {
      clearInterval(this.connectionCheckInterval);
      this.connectionCheckInterval = null;
    }
  }
}

// ============================================================================
// SINGLETON INSTANCE
// ============================================================================

let ipcServiceInstance: IpcService | null = null;

/**
 * Get the singleton IPC service instance
 */
export function getIpcService(): IpcService {
  if (!ipcServiceInstance) {
    ipcServiceInstance = new IpcService();
  }
  return ipcServiceInstance;
}

/**
 * Reset the singleton instance (useful for testing)
 */
export function resetIpcService(): void {
  ipcServiceInstance?.dispose();
  ipcServiceInstance = null;
}
