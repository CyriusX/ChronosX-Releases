/**
 * useFocusMode Hook - React integration for Focus Mode state
 *
 * SOLID:
 * - SRP: Only manages Focus Mode state and commands
 * - DIP: Depends on useIpc abstraction
 * - OCP: Extensible for new Focus Mode features
 *
 * CX-139: Frontend Card Timer com Focus Mode
 */

import { useCallback, useEffect, useState } from 'react';
import { useIpc } from './useIpc';
import type {
  FocusModeSnapshot,
  FocusModeState,
  FocusModeType,
} from '../types/ipc';

// ============================================================================
// TYPES
// ============================================================================

export interface UseFocusModeReturn {
  // State
  focusState: FocusModeSnapshot | null;
  isLoading: boolean;
  error: string | null;

  // Computed values
  isFocusRunning: boolean;
  isBreakRunning: boolean;
  isPaused: boolean;
  isOff: boolean;
  progress: number; // 0-100

  // Display values (locally calculated for smooth countdown)
  displayRemainingMs: number;
  displayProgress: number;

  // Actions
  startFocus: () => Promise<void>;
  stopFocus: () => Promise<void>;
  pauseFocus: () => Promise<void>;
  resumeFocus: () => Promise<void>;
  skipBreak: () => Promise<void>;
  refresh: () => Promise<void>;
}

// ============================================================================
// INITIAL STATE
// ============================================================================

const initialSnapshot: FocusModeSnapshot = {
  state: 'Off',
  mode: 'None',
  remainingMs: 0,
  cycleNumber: 0,
  totalCyclesToday: 0,
  nextBreakType: 'Short',
  plannedDurationMs: 0,
  allowUserOverride: true,
  timestamp: new Date().toISOString(),
};

// ============================================================================
// HOOK
// ============================================================================

/**
 * Hook for Focus Mode state management
 *
 * Automatically handles:
 * - Event subscription for focusModeStateChanged
 * - Initial state sync via query
 * - All Focus Mode commands
 * - Local countdown timer for smooth UI updates
 */
export function useFocusMode(): UseFocusModeReturn {
  const { sendCommand, sendQuery, subscribeToEvent, isConnected } = useIpc();

  const [focusState, setFocusState] = useState<FocusModeSnapshot | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [displayRemainingMs, setDisplayRemainingMs] = useState(0);

  // Subscribe to focus mode state changes
  useEffect(() => {
    if (!isConnected) return;

    console.log('[useFocusMode] Subscribing to focusModeStateChanged events');

    const unsubscribe = subscribeToEvent(
      'focusModeStateChanged',
      (payload) => {
        console.log('[useFocusMode] focusModeStateChanged event received:', payload);
        setFocusState({
          state: payload.state,
          mode: payload.mode,
          remainingMs: payload.remainingMs,
          cycleNumber: payload.cycleNumber,
          totalCyclesToday: payload.totalCyclesToday,
          nextBreakType: payload.nextBreakType,
          cycleStartedAt: payload.cycleStartedAt,
          plannedDurationMs: payload.plannedDurationMs,
          allowUserOverride: payload.allowUserOverride,
          timestamp: payload.timestamp,
        });
      }
    );

    return () => {
      console.log('[useFocusMode] Unsubscribing from focusModeStateChanged events');
      unsubscribe();
    };
  }, [subscribeToEvent, isConnected]);

  // Fetch initial state on mount
  useEffect(() => {
    if (!isConnected) return;

    const fetchInitialState = async () => {
      setIsLoading(true);
      setError(null);

      const response = await sendQuery('getFocusModeState');

      if (response.success && response.data) {
        setFocusState(response.data);
      } else {
        setError(response.error ?? 'Failed to fetch focus mode state');
        // Use initial state as fallback
        setFocusState(initialSnapshot);
      }

      setIsLoading(false);
    };

    fetchInitialState();
  }, [sendQuery, isConnected]);

  // Helper to fetch updated state after a successful command
  const fetchAndUpdateState = useCallback(async () => {
    const stateResponse = await sendQuery('getFocusModeState');
    if (stateResponse.success && stateResponse.data) {
      console.log('[useFocusMode] Fetched updated state:', stateResponse.data);
      setFocusState(stateResponse.data);
    }
  }, [sendQuery]);

  // Actions
  const startFocus = useCallback(async () => {
    console.log('[useFocusMode] startFocus called');
    setError(null);
    const response = await sendCommand('startFocusMode');
    console.log('[useFocusMode] startFocus response:', response);
    if (!response.success) {
      setError(response.error ?? 'Failed to start focus mode');
    } else {
      // Fetch updated state to ensure UI reflects the change
      await fetchAndUpdateState();
    }
  }, [sendCommand, fetchAndUpdateState]);

  const stopFocus = useCallback(async () => {
    setError(null);
    const response = await sendCommand('stopFocusMode');
    if (!response.success) {
      setError(response.error ?? 'Failed to stop focus mode');
    } else {
      await fetchAndUpdateState();
    }
  }, [sendCommand, fetchAndUpdateState]);

  const pauseFocus = useCallback(async () => {
    setError(null);
    const response = await sendCommand('pauseFocusMode');
    if (!response.success) {
      setError(response.error ?? 'Failed to pause focus mode');
    } else {
      await fetchAndUpdateState();
    }
  }, [sendCommand, fetchAndUpdateState]);

  const resumeFocus = useCallback(async () => {
    setError(null);
    const response = await sendCommand('resumeFocusMode');
    if (!response.success) {
      setError(response.error ?? 'Failed to resume focus mode');
    } else {
      await fetchAndUpdateState();
    }
  }, [sendCommand, fetchAndUpdateState]);

  const skipBreak = useCallback(async () => {
    setError(null);
    const response = await sendCommand('skipBreak');
    if (!response.success) {
      setError(response.error ?? 'Failed to skip break');
    } else {
      await fetchAndUpdateState();
    }
  }, [sendCommand, fetchAndUpdateState]);

  const refresh = useCallback(async () => {
    if (!isConnected) return;

    const response = await sendQuery('getFocusModeState');
    if (response.success && response.data) {
      setFocusState(response.data);
    }
  }, [sendQuery, isConnected]);

  // Local countdown timer - updates every second for smooth UI
  useEffect(() => {
    const isActive = focusState?.state === 'FocusRunning' || focusState?.state === 'BreakRunning';

    if (!isActive || !focusState.cycleStartedAt || focusState.plannedDurationMs <= 0) {
      // Not running or missing data - use backend value directly
      setDisplayRemainingMs(focusState?.remainingMs ?? 0);
      return;
    }

    const startTime = new Date(focusState.cycleStartedAt).getTime();
    const plannedDuration = focusState.plannedDurationMs;

    const updateDisplay = () => {
      const now = Date.now();
      const elapsed = now - startTime;
      const remaining = Math.max(0, plannedDuration - elapsed);
      setDisplayRemainingMs(remaining);
    };

    // Update immediately
    updateDisplay();

    // Then update every second
    const intervalId = setInterval(updateDisplay, 1000);

    return () => {
      clearInterval(intervalId);
    };
  }, [focusState?.state, focusState?.cycleStartedAt, focusState?.plannedDurationMs, focusState?.remainingMs]);

  // Computed values
  const currentState = focusState?.state ?? 'Off';
  const isFocusRunning = currentState === 'FocusRunning';
  const isBreakRunning = currentState === 'BreakRunning';
  const isPaused = currentState === 'FocusPaused';
  const isOff = currentState === 'Off';

  // Calculate progress (0-100) - from backend state
  const progress =
    focusState && focusState.plannedDurationMs > 0
      ? Math.max(
          0,
          Math.min(
            100,
            ((focusState.plannedDurationMs - focusState.remainingMs) / focusState.plannedDurationMs) * 100
          )
        )
      : 0;

  // Calculate display progress (0-100) - from local countdown
  const displayProgress =
    focusState && focusState.plannedDurationMs > 0
      ? Math.max(
          0,
          Math.min(
            100,
            ((focusState.plannedDurationMs - displayRemainingMs) / focusState.plannedDurationMs) * 100
          )
        )
      : 0;

  return {
    focusState,
    isLoading,
    error,
    isFocusRunning,
    isBreakRunning,
    isPaused,
    isOff,
    progress,
    displayRemainingMs,
    displayProgress,
    startFocus,
    stopFocus,
    pauseFocus,
    resumeFocus,
    skipBreak,
    refresh,
  };
}

// ============================================================================
// UTILITY FUNCTIONS
// ============================================================================

/**
 * Formats remaining time in MM:SS or HH:MM:SS format
 * Based on focus mode type
 */
export function formatRemaining(
  remainingMs: number,
  mode: FocusModeType
): string {
  const totalSec = Math.ceil(remainingMs / 1000);
  const h = Math.floor(totalSec / 3600);
  const m = Math.floor((totalSec % 3600) / 60);
  const s = totalSec % 60;

  // Ultradian (90 min) or any duration > 1 hour -> HH:MM:SS
  if (mode === 'Ultradian' || h > 0) {
    return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  }

  // Pomodoro (25 min) -> MM:SS
  return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
}

/**
 * Gets the display label for a focus mode state
 */
export function getFocusStateLabel(state: FocusModeState): string {
  switch (state) {
    case 'FocusRunning':
      return 'Em foco';
    case 'FocusPaused':
      return 'Pausado';
    case 'BreakRunning':
      return 'Em pausa';
    case 'Off':
    default:
      return 'Desativado';
  }
}

/**
 * Gets the emoji for a focus mode type
 */
export function getFocusModeEmoji(mode: FocusModeType): string {
  switch (mode) {
    case 'Pomodoro':
      return '\u{1F345}'; // Tomato
    case 'Ultradian':
      return '\u{1F30A}'; // Wave
    default:
      return '';
  }
}

/**
 * Gets the label for a break type
 */
export function getBreakTypeLabel(type: 'Short' | 'Long'): string {
  return type === 'Long' ? 'Pausa longa' : 'Pausa curta';
}
