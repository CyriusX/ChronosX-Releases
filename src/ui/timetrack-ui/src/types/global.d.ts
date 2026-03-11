import type { IpcResponse, IpcEvent } from './ipc';

declare global {
  interface Window {
    timeTrackBridge?: {
      /**
       * Whether connected to AgentService
       */
      isConnected: boolean;

      /**
       * Send a command to AgentService (fire-and-forget)
       */
      sendCommand: (
        command: string,
        payloadJson?: string,
        callbackId?: string
      ) => void;

      /**
       * Send a query to AgentService and receive response
       */
      sendQuery: (
        query: string,
        payloadJson?: string,
        callbackId?: string
      ) => void;

      /**
       * Reconnect to AgentService
       */
      reconnect: (callbackId?: string) => void;

      /**
       * Open external URL in default browser
       */
      openExternal: (url: string) => void;

      /**
       * Called when bridge is ready
       */
      onReady?: () => void;

      /**
       * Called when an event is received from AgentService
       */
      onEvent?: (event: IpcEvent) => void;

      /**
       * Called when connection state changes
       */
      onConnectionStateChanged?: (isConnected: boolean) => void;
    };
  }
}

export {};
