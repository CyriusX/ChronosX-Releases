/**
 * IPC Types for WebView2 Bridge Communication
 * These types match the C# WebViewBridge interface
 *
 * SOLID: ISP - Interfaces segregadas por responsabilidade
 */

import type { AppLanguage, FocusModePolicy } from './settings';

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
  | 'focusModeStateChanged'
  | 'agentHealthChanged'
  | 'updateAvailable'
  | 'updateProgress'
  | 'updateComplete'
  | 'updateFailed';

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

// ============================================================================
// FOCUS MODE TYPES (CX-139)
// ============================================================================

/**
 * Focus mode state - matches C# FocusModeState enum
 */
export type FocusModeState = 'Off' | 'FocusRunning' | 'FocusPaused' | 'BreakRunning';

/**
 * Focus mode type - matches C# FocusModeType enum
 */
export type FocusModeType = 'None' | 'Pomodoro' | 'Ultradian';

/**
 * Break type - matches C# BreakType enum
 */
export type BreakType = 'Short' | 'Long';

/**
 * Snapshot of the FocusModeEngine state
 * Matches C# FocusModeSnapshot class
 */
export interface FocusModeSnapshot {
  state: FocusModeState;
  mode: FocusModeType;
  remainingMs: number;
  cycleNumber: number;
  totalCyclesToday: number;
  nextBreakType: BreakType;
  cycleStartedAt?: string;
  plannedDurationMs: number;
  allowUserOverride: boolean;
  timestamp: string;
}

/**
 * Payload for focusModeStateChanged event
 * Includes both current state and transition info
 */
export interface FocusModeStateChangedPayload extends FocusModeSnapshot {
  previousState: FocusModeState;
  reason: string;
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

// Kanban task events (Phase 4 integration)
export interface NotificationReceivedPayload {
  kind: string;
  taskId?: string | null;
  projectId?: string | null;
}

export interface MyTasksChangedPayload {
  kind: string;
  taskId?: string | null;
}

export interface TaskIdleAutoPausedPayload {
  taskId: string;
  taskTitle: string;
  projectName: string;
}

// ============================================================================
// UPDATE EVENT PAYLOADS
// ============================================================================

export interface UpdateAvailablePayload {
  hasUpdate: boolean;
  currentVersion: string;
  latestVersion: string;
  fileSizeBytes: number;
  releaseNotes: string;
}

export interface UpdateProgressPayload {
  stage: string;
  percentage: number;
  message: string;
  bytesDownloaded?: number;
  bytesTotal?: number;
  targetVersion?: string;
}

export interface UpdateCompletePayload {
  version: string;
}

export interface UpdateFailedPayload {
  error: string;
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
  focusModeStateChanged: FocusModeStateChangedPayload;
  agentHealthChanged: AgentHealthChangedPayload;
  connectionStateChanged: ConnectionStateChangedPayload;
  notificationReceived: NotificationReceivedPayload;
  myTasksChanged: MyTasksChangedPayload;
  taskIdleAutoPaused: TaskIdleAutoPausedPayload;
  updateAvailable: UpdateAvailablePayload;
  updateProgress: UpdateProgressPayload;
  updateComplete: UpdateCompletePayload;
  updateFailed: UpdateFailedPayload;
}

// ============================================================================
// COMMAND TYPES
// ============================================================================

export type AgentCommand =
  | 'storeTokens'
  | 'startTracking'
  | 'stopTracking'
  | 'pauseTracking'
  | 'resumeTracking'
  | 'startFocusMode'
  | 'stopFocusMode'
  | 'pauseFocusMode'
  | 'resumeFocusMode'
  | 'skipBreak'
  | 'assignProject'
  | 'assignTask'
  | 'syncNow'
  | 'updateSettings'
  | 'setWorkHours'
  | 'recordFocusSession'
  | 'checkForUpdates'
  | 'startUpdate';

// Command Payloads
export interface StoreTokensPayload {
  accessToken: string;
  refreshToken: string;
}

export interface PauseTrackingPayload {
  reason?: string;
}

export interface UpdateAppCategoryPayload {
  displayName: string;    // Display name from resolver (e.g., "VS Code")
  identifier: string;     // Normalized identifier (e.g., "code.exe")
  productivity: string;   // "productive" | "neutral" | "distraction"
  subcategory: string;
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
  // Legacy fields (deprecated)
  idleTimeoutMinutes?: number;
  autoPauseOnIdle?: boolean;
  syncIntervalMinutes?: number;
  notificationsEnabled?: boolean;

  // Local settings fields
  autoResumeNotificationEnabled?: boolean;
  notificationSoundsEnabled?: boolean;
  language?: AppLanguage;
  idleThresholdSeconds?: number;
  workGoalSeconds?: number;
}

export interface SetWorkHoursPayload {
  startHour: number; // 0-23
  endHour: number; // 0-23
  workDays: number[]; // 0=Sunday, 6=Saturday
  timezone?: string;
}

export interface RecordFocusSessionPayload {
  id: string;
  startedAt: string;
  completedAt: string;
  durationMs: number;
  mode: string;
  cycle: number;
  name?: string;
  productivity: number;
}

// Command payload map for type-safe commands
export interface CommandPayloadMap {
  storeTokens: StoreTokensPayload;
  startTracking: undefined;
  stopTracking: undefined;
  pauseTracking: PauseTrackingPayload;
  resumeTracking: undefined;
  startFocusMode: undefined;
  stopFocusMode: undefined;
  pauseFocusMode: undefined;
  resumeFocusMode: undefined;
  skipBreak: undefined;
  applyFocusPolicy: FocusModePolicy; // CX-139: Apply policy to AgentService
  assignProject: AssignProjectPayload;
  assignTask: AssignTaskPayload;
  syncNow: undefined;
  updateAppCategory: UpdateAppCategoryPayload;
  updateSettings: UpdateSettingsPayload;
  setWorkHours: SetWorkHoursPayload;
  recordFocusSession: RecordFocusSessionPayload;
  checkForUpdates: undefined;
  startUpdate: undefined;
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
  | 'getErrors'
  | 'getSettings'
  | 'getFocusModeState';

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
  focusScore: number;
  sessionsCount: number;
  topProjects: ProjectSummary[];
  topApplications: ApplicationSummary[];
  topAppsByExe?: ApplicationSummary[];
  categories: CategorySummary[];
  weeklyHistory: WeeklyHistoryItem[];
}

export interface CategorySummary {
  name: string;
  duration: number;
  percentage: number;
  color: string;
  subcategory?: string;
  productivity?: AppProductivityCategory;
  source?: 'global' | 'org_override' | 'default';
}

// CX-143: App Productivity Category
export type AppProductivityCategory = 'productive' | 'neutral' | 'distraction';

// CX-143: App Category Response from Backend
export interface AppCategoryResponse {
  identifier: string;
  identifierType: 'exe' | 'domain';
  displayName: string;
  productivity: AppProductivityCategory;
  subcategory: string;
  source: 'global' | 'org_override' | 'default';
  note?: string;
}

// CX-143: Category Usage Stats for Admin Dashboard
export interface CategoryUsageStatsResponse {
  topProductiveApps: CategoryUsageItem[];
  topNeutralApps: CategoryUsageItem[];
  topDistractionApps: CategoryUsageItem[];
  uncategorizedApps: UncategorizedAppItem[];
  totalAppsUsed: number;
  categorizedApps: number;
  uncategorizedCount: number;
}

export interface CategoryUsageItem {
  identifier: string;
  displayName: string;
  productivity: AppProductivityCategory;
  subcategory: string;
  totalMinutes: number;
  sessionCount: number;
  source: string;
}

export interface UncategorizedAppItem {
  identifier: string;
  identifierType: 'exe' | 'domain';
  totalMinutes: number;
  sessionCount: number;
  userCount: number;
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
  // CX-143: Productivity classification
  productivity?: AppProductivityCategory;
  subcategory?: string;
  source?: 'global' | 'org_override' | 'default';
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
  desktopHostVersion?: string;
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

// ============================================================================
// LOCAL SETTINGS TYPES
// ============================================================================

export interface LocalSettingsResponse {
  autoResumeNotificationEnabled: boolean;
  notificationSoundsEnabled: boolean;
  language: AppLanguage;
  idleThresholdSeconds: number | null;
  workGoalSeconds: number | null;
  updatedAt: string;
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
  getSettings: LocalSettingsResponse;
  getFocusModeState: FocusModeSnapshot;
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
    query: K,
    payloadJson?: string
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
