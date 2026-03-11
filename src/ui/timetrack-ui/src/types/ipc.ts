/**
 * IPC Types for WebView2 Bridge Communication
 * These types match the C# WebViewBridge interface
 *
 * SOLID: ISP - Interfaces segregadas por responsabilidade
 */

// ============================================================================
// BASE TYPES
// ============================================================================

export interface IpcResponse<T = unknown> {
  callbackId?: string;
  success: boolean;
  data?: T;
  error?: string;
}

export interface IpcEvent<T = unknown> {
  eventType: string;
  payload: T;
}

// ============================================================================
// EVENT TYPES (AgentEventType)
// ============================================================================

export type AgentEventType =
  | 'trackingStarted'
  | 'trackingStopped'
  | 'trackingStateChanged'
  | 'activityChanged'
  | 'idleStateChanged'
  | 'sessionUpdated'
  | 'syncCompleted'
  | 'syncProgressChanged'
  | 'connectionStateChanged'
  | 'focusModeChanged'
  | 'agentHealthChanged';

// ============================================================================
// EVENT PAYLOADS
// ============================================================================

export interface TrackingStartedPayload {
  sessionId: string;
  timestamp: string;
}

export interface TrackingStoppedPayload {
  sessionId: string;
  totalDuration: number;
  timestamp: string;
}

export interface TrackingStateChangedPayload {
  isTracking: boolean;
  isPaused: boolean;
  reason?: string;
}

export interface ActivityChangedPayload {
  processName: string;
  windowTitle: string;
  exePath: string;
  timestamp: string;
}

export interface IdleStateChangedPayload {
  isIdle: boolean;
  idleTimeSeconds: number;
}

export interface SessionUpdatedPayload {
  sessionId: string;
  projectName?: string;
  taskName?: string;
  duration: number;
}

export interface SyncCompletedPayload {
  success: boolean;
  syncedItems: number;
  timestamp: string;
}

export interface SyncProgressChangedPayload {
  progress: number; // 0-100
  status: 'pending' | 'in_progress' | 'completed' | 'failed';
  message?: string;
}

export interface FocusModeChangedPayload {
  isActive: boolean;
  startedAt?: string;
  duration?: number;
}

export interface AgentHealthChangedPayload {
  status: 'healthy' | 'degraded' | 'unhealthy';
  cpuUsage?: number;
  memoryUsage?: number;
  lastSync?: string;
  errors?: string[];
}

export interface ConnectionStateChangedPayload {
  isConnected: boolean;
  reconnectAttempts?: number;
}

// Event payload map for type-safe event handling
export interface EventPayloadMap {
  trackingStarted: TrackingStartedPayload;
  trackingStopped: TrackingStoppedPayload;
  trackingStateChanged: TrackingStateChangedPayload;
  activityChanged: ActivityChangedPayload;
  idleStateChanged: IdleStateChangedPayload;
  sessionUpdated: SessionUpdatedPayload;
  syncCompleted: SyncCompletedPayload;
  syncProgressChanged: SyncProgressChangedPayload;
  focusModeChanged: FocusModeChangedPayload;
  agentHealthChanged: AgentHealthChangedPayload;
  connectionStateChanged: ConnectionStateChangedPayload;
}

// ============================================================================
// COMMAND TYPES
// ============================================================================

export type AgentCommand =
  | 'startTracking'
  | 'stopTracking'
  | 'pauseTracking'
  | 'resumeTracking'
  | 'startFocusMode'
  | 'stopFocusMode'
  | 'assignProject'
  | 'assignTask'
  | 'syncNow'
  | 'updateSettings'
  | 'setWorkHours';

// Command Payloads
export interface PauseTrackingPayload {
  reason?: string;
}

export interface AssignProjectPayload {
  projectId: string;
  sessionId?: string;
}

export interface AssignTaskPayload {
  taskId: string;
  sessionId?: string;
}

export interface UpdateSettingsPayload {
  idleTimeoutMinutes?: number;
  autoPauseOnIdle?: boolean;
  syncIntervalMinutes?: number;
  notificationsEnabled?: boolean;
}

export interface SetWorkHoursPayload {
  startHour: number; // 0-23
  endHour: number; // 0-23
  workDays: number[]; // 0=Sunday, 6=Saturday
  timezone?: string;
}

// Command payload map for type-safe commands
export interface CommandPayloadMap {
  startTracking: undefined;
  stopTracking: undefined;
  pauseTracking: PauseTrackingPayload;
  resumeTracking: undefined;
  startFocusMode: undefined;
  stopFocusMode: undefined;
  assignProject: AssignProjectPayload;
  assignTask: AssignTaskPayload;
  syncNow: undefined;
  updateSettings: UpdateSettingsPayload;
  setWorkHours: SetWorkHoursPayload;
}

// ============================================================================
// QUERY TYPES
// ============================================================================

export type AgentQuery =
  | 'getCurrentSession'
  | 'getTodaySummary'
  | 'getRecentActivities'
  | 'getRecentApps'
  | 'getProjects'
  | 'getTasks'
  | 'getTrackingState'
  | 'getCurrentStatus'
  | 'getSyncState'
  | 'getErrors';

// ============================================================================
// QUERY RESPONSE TYPES
// ============================================================================

export interface CurrentSessionResponse {
  id: string;
  projectName?: string;
  taskName?: string;
  startedAt: string;
  duration: number;
  isIdle: boolean;
  isActive: boolean;
}

export interface TodaySummaryResponse {
  totalDuration: number;
  productiveTime: number;
  idleTime: number;
  focusTime: number;
  sessionsCount: number;
  topProjects: ProjectSummary[];
  topApplications: ApplicationSummary[];
  categories: CategorySummary[];
  weeklyHistory: WeeklyHistoryItem[];
}

export interface CategorySummary {
  name: string;
  duration: number;
  percentage: number;
  color: string;
}

export interface WeeklyHistoryItem {
  date: string;
  dayName: string;
  hours: number;
  isToday: boolean;
}

export interface ProjectSummary {
  name: string;
  duration: number;
  percentage: number;
}

export interface ApplicationSummary {
  name: string;
  duration: number;
  percentage: number;
  iconPath?: string;
}

export interface RecentActivityResponse {
  activities: ActivityItem[];
  total: number;
}

export interface ActivityItem {
  id: string;
  processName: string;
  windowTitle: string;
  exePath: string;
  startedAt: string;
  endedAt?: string;
  duration: number;
  projectName?: string;
  taskId?: string;
}

export interface RecentAppItem {
  processName: string;
  windowTitle: string;
  exePath: string;
  totalDuration: number;
  sessionCount: number;
  lastSeen: string;
}

export interface RecentAppsResponse {
  apps: RecentAppItem[];
  since: string;
}

export interface TrackingStateResponse {
  isTracking: boolean;
  isPaused: boolean;
  isFocusMode: boolean;
  currentSession?: CurrentSessionResponse;
}

export interface CurrentStatusResponse {
  state: 'running' | 'paused' | 'stopped' | 'idle';
  uptime: number; // seconds
  version: string;
  sessionId?: string;
  lastActivity?: string;
}

export interface SyncStateResponse {
  status: 'synced' | 'pending' | 'syncing' | 'failed';
  lastSyncAt?: string;
  pendingItems: number;
  failedItems: number;
  nextSyncAt?: string;
}

export interface ErrorItem {
  id: string;
  timestamp: string;
  type: string;
  message: string;
  details?: string;
  resolved: boolean;
}

export interface ErrorsResponse {
  errors: ErrorItem[];
  total: number;
}

export interface ProjectResponse {
  id: string;
  name: string;
  color?: string;
  client?: string;
}

export interface TaskResponse {
  id: string;
  name: string;
  projectId: string;
  status: 'pending' | 'in_progress' | 'completed';
}

// Query response map for type-safe queries
export interface QueryResponseMap {
  getCurrentSession: CurrentSessionResponse;
  getTodaySummary: TodaySummaryResponse;
  getRecentActivities: RecentActivityResponse;
  getRecentApps: RecentAppsResponse;
  getProjects: ProjectResponse[];
  getTasks: TaskResponse[];
  getTrackingState: TrackingStateResponse;
  getCurrentStatus: CurrentStatusResponse;
  getSyncState: SyncStateResponse;
  getErrors: ErrorsResponse;
}

// ============================================================================
// BRIDGE INTERFACE (WebView2)
// ============================================================================

/**
 * Interface for the WebView2 bridge object
 * DIP: Components depend on this interface, not the concrete implementation
 */
export interface ITimeTrackBridge {
  readonly isConnected: boolean;

  sendCommand(command: string, payloadJson?: string): Promise<IpcResponse>;
  sendQuery(query: string, payloadJson?: string): Promise<IpcResponse>;
  subscribeToEvent?(eventType: string, callback: (payloadJson: string) => void): () => void;
}

// Extend window type for bridge
declare global {
  interface Window {
    timeTrackBridge?: ITimeTrackBridge;
  }
}

// ============================================================================
// IPC SERVICE INTERFACES (SOLID: ISP)
// ============================================================================

/**
 * Interface for sending commands - ISP: segregated from full IPC
 */
export interface ICommandSender {
  sendCommand<K extends keyof CommandPayloadMap>(
    command: K,
    payload?: CommandPayloadMap[K]
  ): Promise<IpcResponse<void>>;
}

/**
 * Interface for sending queries - ISP: segregated from full IPC
 */
export interface IQuerySender {
  sendQuery<K extends keyof QueryResponseMap>(
    query: K
  ): Promise<IpcResponse<QueryResponseMap[K]>>;
}

/**
 * Interface for event subscription - ISP: segregated from full IPC
 */
export interface IEventSubscriber {
  subscribe<K extends keyof EventPayloadMap>(
    eventType: K,
    callback: (payload: EventPayloadMap[K]) => void
  ): () => void;
}

/**
 * Interface for connection management - ISP: segregated
 */
export interface IConnectionManager {
  readonly isConnected: boolean;
  readonly isReady: boolean;
  readonly connectionState: ConnectionState;
  onConnectionChange(callback: (isConnected: boolean) => void): () => void;
  reconnect(): Promise<void>;
}

export type ConnectionState =
  | 'disconnected'
  | 'connecting'
  | 'connected'
  | 'reconnecting';

/**
 * Combined IPC client interface - composition of segregated interfaces
 * DIP: High-level modules depend on this abstraction
 */
export interface IIpcClient
  extends ICommandSender,
    IQuerySender,
    IEventSubscriber,
    IConnectionManager {}
