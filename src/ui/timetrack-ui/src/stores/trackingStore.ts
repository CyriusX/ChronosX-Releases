/**
 * Tracking Store - Zustand state management
 *
 * SOLID:
 * - SRP: Only manages tracking-related state
 * - OCP: Extensible via new state slices
 * - DIP: Uses subscribeWithSelector for fine-grained subscriptions
 */

import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type {
  TrackingStateResponse,
  CurrentSessionResponse,
  TodaySummaryResponse,
  ActivityItem,
  CategorySummary,
  WeeklyHistoryItem,
  SyncStateResponse,
  CurrentStatusResponse,
  ErrorItem,
} from '../types/ipc';

// ============================================================================
// STATE INTERFACE
// ============================================================================

interface TrackingState {
  // Connection state
  isConnected: boolean;
  isReady: boolean;
  connectionState: 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

  // Tracking state
  isTracking: boolean;
  isPaused: boolean;
  isFocusMode: boolean;

  // Current session
  currentSession: CurrentSessionResponse | null;

  // Today's data
  todaySummary: TodaySummaryResponse | null;

  // Weekly history for chart
  weeklyHistory: WeeklyHistoryItem[];

  // Categories for dashboard
  categories: CategorySummary[];

  // Recent activities
  recentActivities: ActivityItem[];
  isLoadingActivities: boolean;

  // Sync state
  syncState: SyncStateResponse | null;

  // Agent status
  agentStatus: CurrentStatusResponse | null;

  // Errors
  recentErrors: ErrorItem[];

  // UI state
  sidebarCollapsed: boolean;
  focusModeDialogOpen: boolean;
  assignProjectDialogOpen: boolean;

  // Actions - Connection
  setConnected: (connected: boolean) => void;
  setReady: (ready: boolean) => void;
  setConnectionState: (state: TrackingState['connectionState']) => void;

  // Actions - Tracking
  setTrackingState: (state: Partial<TrackingStateResponse>) => void;
  setCurrentSession: (session: CurrentSessionResponse | null) => void;
  setPaused: (paused: boolean) => void;
  setTracking: (tracking: boolean) => void;
  setFocusMode: (focusMode: boolean) => void;

  // Actions - Data
  setTodaySummary: (summary: TodaySummaryResponse) => void;
  setWeeklyHistory: (history: WeeklyHistoryItem[]) => void;
  setCategories: (categories: CategorySummary[]) => void;
  setRecentActivities: (activities: ActivityItem[]) => void;
  setLoadingActivities: (loading: boolean) => void;

  // Actions - Sync & Status
  setSyncState: (state: SyncStateResponse) => void;
  setAgentStatus: (status: CurrentStatusResponse) => void;
  setRecentErrors: (errors: ErrorItem[]) => void;
  addError: (error: ErrorItem) => void;
  clearErrors: () => void;

  // Actions - UI
  toggleSidebar: () => void;
  setFocusModeDialogOpen: (open: boolean) => void;
  setAssignProjectDialogOpen: (open: boolean) => void;

  // Actions - Reset
  reset: () => void;
}

// ============================================================================
// INITIAL STATE
// ============================================================================

const initialState = {
  // Connection
  isConnected: false,
  isReady: false,
  connectionState: 'disconnected' as const,

  // Tracking
  isTracking: false,
  isPaused: false,
  isFocusMode: false,
  currentSession: null,

  // Data
  todaySummary: null,
  weeklyHistory: [],
  categories: [],
  recentActivities: [],
  isLoadingActivities: false,

  // Sync & Status
  syncState: null,
  agentStatus: null,
  recentErrors: [],

  // UI
  sidebarCollapsed: false,
  focusModeDialogOpen: false,
  assignProjectDialogOpen: false,
};

// ============================================================================
// STORE
// ============================================================================

export const useTrackingStore = create<TrackingState>()(
  subscribeWithSelector((set) => ({
    ...initialState,

    // Connection Actions
    setConnected: (connected) => set({ isConnected: connected }),
    setReady: (ready) => set({ isReady: ready }),
    setConnectionState: (connectionState) => set({ connectionState }),

    // Tracking Actions
    setTrackingState: (state) =>
      set((prev) => ({
        isTracking: state.isTracking ?? prev.isTracking,
        isPaused: state.isPaused ?? prev.isPaused,
        isFocusMode: state.isFocusMode ?? prev.isFocusMode,
        currentSession: state.currentSession ?? prev.currentSession,
      })),

    setCurrentSession: (session) => set({ currentSession: session }),

    setPaused: (paused) => set({ isPaused: paused }),

    setTracking: (tracking) => set({ isTracking: tracking }),

    setFocusMode: (focusMode) => set({ isFocusMode: focusMode }),

    // Data Actions
    setTodaySummary: (summary) =>
      set({
        todaySummary: summary,
        categories: summary?.categories ?? [],
        weeklyHistory: summary?.weeklyHistory ?? [],
      }),

    setWeeklyHistory: (history) => set({ weeklyHistory: history }),

    setCategories: (categories) => set({ categories }),

    setRecentActivities: (activities) => set({ recentActivities: activities }),

    setLoadingActivities: (loading) => set({ isLoadingActivities: loading }),

    // Sync & Status Actions
    setSyncState: (syncState) => set({ syncState }),

    setAgentStatus: (agentStatus) => set({ agentStatus }),

    setRecentErrors: (recentErrors) => set({ recentErrors }),

    addError: (error) =>
      set((state) => ({
        recentErrors: [error, ...state.recentErrors].slice(0, 50), // Keep last 50
      })),

    clearErrors: () => set({ recentErrors: [] }),

    // UI Actions
    toggleSidebar: () =>
      set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),

    setFocusModeDialogOpen: (open) => set({ focusModeDialogOpen: open }),

    setAssignProjectDialogOpen: (open) => set({ assignProjectDialogOpen: open }),

    // Reset
    reset: () => set(initialState),
  }))
);

// ============================================================================
// SELECTORS (for memoization)
// ============================================================================

export const selectIsConnected = (state: TrackingState) => state.isConnected;
export const selectIsTracking = (state: TrackingState) => state.isTracking;
export const selectIsPaused = (state: TrackingState) => state.isPaused;
export const selectCurrentSession = (state: TrackingState) => state.currentSession;
export const selectTodaySummary = (state: TrackingState) => state.todaySummary;
export const selectRecentActivities = (state: TrackingState) => state.recentActivities;
export const selectSyncState = (state: TrackingState) => state.syncState;
export const selectAgentStatus = (state: TrackingState) => state.agentStatus;
export const selectRecentErrors = (state: TrackingState) => state.recentErrors;

// Composite selectors
export const selectTrackingStatus = (state: TrackingState) => ({
  isTracking: state.isTracking,
  isPaused: state.isPaused,
  isFocusMode: state.isFocusMode,
});

export const selectConnectionStatus = (state: TrackingState) => ({
  isConnected: state.isConnected,
  isReady: state.isReady,
  connectionState: state.connectionState,
});

// ============================================================================
// EVENT HANDLERS (for automatic store updates from IPC events)
// ============================================================================

/**
 * Handler for trackingStateChanged event
 */
export function handleTrackingStateChanged(
  payload: { isTracking: boolean; isPaused: boolean; reason?: string }
): void {
  useTrackingStore.getState().setTrackingState({
    isTracking: payload.isTracking,
    isPaused: payload.isPaused,
  });
}

/**
 * Handler for sessionUpdated event
 */
export function handleSessionUpdated(
  payload: { sessionId: string; projectName?: string; taskName?: string; duration: number }
): void {
  const session = useTrackingStore.getState().currentSession;
  if (session && session.id === payload.sessionId) {
    useTrackingStore.getState().setCurrentSession({
      ...session,
      projectName: payload.projectName,
      taskName: payload.taskName,
      duration: payload.duration,
    });
  }
}

/**
 * Handler for syncProgressChanged event
 */
export function handleSyncProgressChanged(
  payload: { progress: number; status: string; message?: string }
): void {
  const currentSync = useTrackingStore.getState().syncState;
  useTrackingStore.getState().setSyncState({
    ...currentSync,
    status: payload.status as SyncStateResponse['status'],
    // Keep other sync state fields
  } as SyncStateResponse);
}

/**
 * Handler for agentHealthChanged event
 */
export function handleAgentHealthChanged(
  _payload: { status: string; cpuUsage?: number; memoryUsage?: number }
): void {
  const currentStatus = useTrackingStore.getState().agentStatus;
  if (currentStatus) {
    useTrackingStore.getState().setAgentStatus({
      ...currentStatus,
      // Update health metrics if needed
    });
  }
}
