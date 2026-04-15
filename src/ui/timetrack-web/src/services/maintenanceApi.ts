import { api } from './apiClient';

// ============================================================================
// TYPES
// ============================================================================

export interface DeviceListItem {
  deviceId: string;
  hostname: string;
  deviceName: string | null;
  agentVersion: string;
  displayMode: string;
  status: string; // "active" | "offline" | "inactive"
  trackingState: string | null; // "running" | "paused" | "stopped" | null
  healthStatus: string | null; // "healthy" | "degraded" | "unhealthy" | "offline" | null
  ipcConnected: boolean | null;
  lastSeenAt: string | null;
  activatedAt: string;
  userDisplayName: string | null;
}

export interface ListDevicesResponse {
  devices: DeviceListItem[];
  totalCount: number;
}

export interface MetricsHistoryPoint {
  sampledAt: string;
  cpuPercent: number;
  memoryUsedMb: number;
}

export interface DeviceMetricsResponse {
  deviceId: string;
  cpuPercent: number;
  memoryUsedMb: number;
  memoryTotalMb: number;
  diskUsedGb: number;
  diskTotalGb: number;
  lastUpdatedAt: string | null;
  recentHistory: MetricsHistoryPoint[];
}

export interface DeviceEventItem {
  id: string;
  eventType: string;
  category: string;
  severity: string;
  message: string;
  metadataJson: string | null;
  timestamp: string;
}

export interface DeviceEventsResponse {
  events: DeviceEventItem[];
  totalCount: number;
}

export interface DeviceInfoResponse {
  deviceId: string;
  hostname: string;
  deviceName: string | null;
  agentVersion: string;
  osVersion: string | null;
  ipAddress: string | null;
  uptimeSeconds: number | null;
  trackingState: string | null;
  healthStatus: string | null;
  consecutiveSyncFailures: number | null;
  lastSuccessfulSyncAt: string | null;
  ipcConnected: boolean | null;
  lastHeartbeatAt: string | null;
  activatedAt: string;
  status: string;
  displayMode: string;
  userDisplayName: string | null;
}

export interface HealthAlertItem {
  deviceId: string;
  hostname: string;
  userDisplayName: string | null;
  issue: string; // "offline" | "degraded" | "unhealthy"
  lastSeenAt: string | null;
  healthStatus: string | null;
}

export interface HealthSummaryResponse {
  totalDevices: number;
  onlineCount: number;
  offlineCount: number;
  degradedCount: number;
  unhealthyCount: number;
  alerts: HealthAlertItem[];
}

export interface CommandHistoryItem {
  id: string;
  commandType: string;
  status: string;
  payloadJson: string | null;
  resultJson: string | null;
  createdAt: string;
  acknowledgedAt: string | null;
}

export interface CommandHistoryResponse {
  commands: CommandHistoryItem[];
}

// ============================================================================
// API FUNCTIONS
// ============================================================================

export async function listOrgDevices(orgId: string): Promise<ListDevicesResponse> {
  return api.get<ListDevicesResponse>(`/orgs/${encodeURIComponent(orgId)}/devices`);
}

export async function getDeviceEvents(
  orgId: string,
  deviceId: string,
  category?: string,
  severity?: string,
  limit: number = 100
): Promise<DeviceEventsResponse> {
  const params = new URLSearchParams();
  if (category) params.set('category', category);
  if (severity) params.set('severity', severity);
  params.set('limit', limit.toString());

  return api.get<DeviceEventsResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/events?${params.toString()}`
  );
}

export async function getDeviceInfo(
  orgId: string,
  deviceId: string
): Promise<DeviceInfoResponse> {
  return api.get<DeviceInfoResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/info`
  );
}

export async function sendRemoteCommand(
  orgId: string,
  deviceId: string,
  commandType: string,
  payload?: object
): Promise<{ commandId: string }> {
  return api.post<{ commandId: string }>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/commands`,
    { commandType, payload }
  );
}

export async function getCommandHistory(
  orgId: string,
  deviceId: string
): Promise<CommandHistoryResponse> {
  return api.get<CommandHistoryResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/commands`
  );
}

export async function clearDeviceEvents(
  orgId: string,
  deviceId: string
): Promise<{ deleted: number }> {
  return api.delete<{ deleted: number }>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/events`
  );
}

export async function clearAllEvents(orgId: string): Promise<{ deleted: number }> {
  return api.delete<{ deleted: number }>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/events`
  );
}

export async function getDeviceMetrics(
  orgId: string,
  deviceId: string
): Promise<DeviceMetricsResponse> {
  return api.get<DeviceMetricsResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/metrics`
  );
}

export async function getHealthSummary(orgId: string): Promise<HealthSummaryResponse> {
  return api.get<HealthSummaryResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/health-summary`
  );
}

export async function deleteDevice(orgId: string, deviceId: string): Promise<void> {
  return api.delete<void>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}`
  );
}
