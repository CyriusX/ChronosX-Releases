/**
 * useIpc — Web stub (no-op)
 *
 * The web admin portal has no local agent. This stub provides the same
 * interface as the desktop useIpc hook but all commands/queries are no-ops.
 * This allows desktop components that optionally use IPC (like useAppCategories)
 * to work in the browser without crashing.
 */

export function useIpc() {
  return {
    isConnected: false,
    isReady: false,
    connectionState: 'disconnected' as const,
    sendCommand: async (_command: string, _payload?: unknown) => ({
      success: false as const,
      error: 'IPC not available in web portal',
    }),
    sendQuery: async (_query: string, _payload?: unknown) => ({
      success: false as const,
      error: 'IPC not available in web portal',
      data: undefined,
    }),
    subscribeToEvent: (_event: string, _handler: (...args: unknown[]) => void) => {
      return () => {}; // unsubscribe no-op
    },
    reconnect: async () => {},
  };
}
