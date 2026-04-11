/**
 * User Integrations API (web). Mirror of the desktop version. The web
 * board shares the same sync contract and renders the "Sync from Linear"
 * button on Linear-synced project boards.
 */

import { api } from './apiClient';

export type IntegrationProvider = 'Linear';
export type IntegrationStatus = 'Active' | 'Revoked' | 'ErrorUnauthorized';

export interface UserIntegration {
  id: string;
  provider: IntegrationProvider;
  status: IntegrationStatus;
  errorMessage: string | null;
  externalUserId: string;
  externalUserName: string | null;
  externalUserEmail: string | null;
  connectedAt: string;
  lastSyncAt: string | null;
  lastUsedAt: string | null;
}

export interface ListUserIntegrationsResponse {
  integrations: UserIntegration[];
}

export interface LinearSyncResult {
  projectsCreated: number;
  projectsUpdated: number;
  tasksCreated: number;
  tasksUpdated: number;
  tasksSoftDeleted: number;
  durationMs: number;
  startedAt: string;
  finishedAt: string;
  integration: UserIntegration;
}

export interface LinearSyncHistoryEntry {
  id: string;
  startedAt: string;
  finishedAt: string;
  durationMs: number;
  projectsCreated: number;
  projectsUpdated: number;
  tasksCreated: number;
  tasksUpdated: number;
  tasksSoftDeleted: number;
  success: boolean;
  errorMessage: string | null;
}

export interface ListLinearSyncHistoryResponse {
  entries: LinearSyncHistoryEntry[];
}

export function listMyIntegrations(): Promise<ListUserIntegrationsResponse> {
  return api.get<ListUserIntegrationsResponse>('/me/integrations');
}

export function connectLinear(apiKey: string): Promise<UserIntegration> {
  return api.post<UserIntegration>('/me/integrations/linear/connect', { apiKey });
}

export function disconnectLinear(): Promise<void> {
  return api.delete<void>('/me/integrations/linear');
}

export function syncLinear(): Promise<LinearSyncResult> {
  return api.post<LinearSyncResult>('/me/integrations/linear/sync');
}

export function getLinearSyncHistory(): Promise<ListLinearSyncHistoryResponse> {
  return api.get<ListLinearSyncHistoryResponse>('/me/integrations/linear/history');
}
