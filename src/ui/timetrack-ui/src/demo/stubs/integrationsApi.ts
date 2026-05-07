import type {
  ListUserIntegrationsResponse,
  LinearSyncResult,
  UserIntegration,
  ListLinearSyncHistoryResponse,
  InitiateLinearOAuthResponse,
} from '../../services/integrationsApi';

const integration: UserIntegration = {
  id: 'int-linear-demo',
  provider: 'Linear',
  status: 'Active',
  errorMessage: null,
  externalUserId: 'linear_user_demo',
  externalUserName: 'Demo',
  externalUserEmail: 'demo@chronosx.app',
  connectedAt: new Date(Date.now() - 30 * 86400_000).toISOString(),
  lastSyncAt: new Date(Date.now() - 25 * 60_000).toISOString(),
  lastUsedAt: new Date(Date.now() - 15 * 60_000).toISOString(),
  authMethod: 'OAuth',
};

export async function listMyIntegrations(): Promise<ListUserIntegrationsResponse> {
  return { integrations: [integration] };
}

export async function connectLinear(_apiKey: string): Promise<UserIntegration> {
  return integration;
}

export async function disconnectLinear(): Promise<void> {}

export async function syncLinear(): Promise<LinearSyncResult> {
  const startedAt = new Date(Date.now() - 1200).toISOString();
  const finishedAt = new Date().toISOString();
  return {
    projectsCreated: 0,
    projectsUpdated: 0,
    tasksCreated: 0,
    tasksUpdated: 3,
    tasksSoftDeleted: 0,
    durationMs: 1200,
    startedAt,
    finishedAt,
    integration,
  };
}

export async function getLinearSyncHistory(): Promise<ListLinearSyncHistoryResponse> {
  return { entries: [] };
}

export async function initiateLinearOAuth(): Promise<InitiateLinearOAuthResponse> {
  return { authorizeUrl: 'https://linear.app', state: 'demo' };
}

