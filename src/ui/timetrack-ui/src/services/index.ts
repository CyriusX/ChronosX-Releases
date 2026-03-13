/**
 * Services barrel export
 */

export {
  IpcService,
  getIpcService,
  resetIpcService,
} from './ipcService';
export type { IpcServiceConfig } from './ipcService';

export { EventDispatcher, globalEventDispatcher } from './eventDispatcher';

export {
  ExponentialBackoffStrategy,
  createDefaultReconnectionStrategy,
} from './reconnectionStrategy';
export type { IReconnectionStrategy, ReconnectionConfig } from './reconnectionStrategy';

// API Services
export * from './memberApi';
export * from './policyApi';
export * from './reportApi';
