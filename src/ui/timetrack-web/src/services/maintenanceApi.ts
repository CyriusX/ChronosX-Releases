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

// ============================================================================
// API FUNCTIONS
// ============================================================================

export async function listOrgDevices(orgId: string): Promise<ListDevicesResponse> {
  return api.get<ListDevicesResponse>(`/orgs/${encodeURIComponent(orgId)}/devices`);
}

export async function getDeviceMetrics(
  orgId: string,
  deviceId: string
): Promise<DeviceMetricsResponse> {
  return api.get<DeviceMetricsResponse>(
    `/orgs/${encodeURIComponent(orgId)}/maintenance/devices/${encodeURIComponent(deviceId)}/metrics`
  );
}
