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

export async function getDeviceMetrics(
  orgId: string,
  deviceId: string
): Promise<DeviceMetricsResponse> {
  return api.get<DeviceMetricsResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/metrics`
  );
}
