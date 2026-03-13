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

      // WebView2 host objects: access method directly via bridge proxy
      // The bridge is chrome.webview.hostObjects.timeTrackBridge injected by MainForm.cs
      const bridgeProxy = bridge as unknown as {
        SendCommand: (command: string, payloadJson?: string) => Promise<string>;
      };

      const responseJson = await bridgeProxy.SendCommand(command, payloadJson);
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
    query: K
  ): Promise<IpcResponse<QueryResponseMap[K]>> {
    const bridge = this.getBridge();

    if (!bridge) {
      console.warn(`[IpcService] Bridge not available for query: ${query}`);
      return { success: false, error: 'Bridge not available' };
    }

    try {
      // WebView2 host objects require awaiting the method reference first
      // chrome.webview.hostObjects.proxy.method is a Promise<Function>
      const sendQueryMethod = await (bridge as unknown as {
        SendQuery: Promise<(query: string, payloadJson?: string) => Promise<string>>;
      }).SendQuery;

      const responseJson = await sendQueryMethod(query, undefined);
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
      console.warn('[IpcService] Bridge not available, running in standalone mode');
      this._connectionState = 'disconnected';
    }

    this.notifyConnectionChange();
  }

  private setupBridgeHandlers(): void {
    (window as unknown as Record<string, unknown>).timeTrackHandleEvent = (
      eventType: string,
      payloadJson: string
    ) => {
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
