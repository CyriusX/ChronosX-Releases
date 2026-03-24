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

// API Client
export { api, apiClient, SESSION_EXPIRED_EVENT, dispatchSessionExpired } from './apiClient';
export type { ApiError } from './apiClient';

// API Services
export * from './memberApi';
export * from './policyApi';
export * from './reportApi';
export * from './appCategoriesApi';
