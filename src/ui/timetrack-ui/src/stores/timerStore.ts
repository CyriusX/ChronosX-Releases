/**
 * Timer Store — Shared Zustand store for Pomodoro / Ultradian timer
 *
 * Both Timer page and Dashboard TimerFocusCard consume this store,
 * keeping the timer in sync across page navigation.
 *
 * The countdown interval runs at the store level (not inside components),
 * so the timer persists when the user navigates between pages or
 * minimizes the app.
 *
 * Uses wall-clock timestamps for remaining-time calculation, so the
 * timer stays accurate even if setInterval is throttled (background tab,
 * OS sleep, etc.).
 */

import { create } from 'zustand';
import { subscribeWithSelector, persist, type PersistStorage } from 'zustand/middleware';
import { getIpcService } from '../services';

// ============================================================================
// TYPES
// ============================================================================

export type TimerMode = 'pomodoro' | 'ultradian';
export type TimerPhase = 'idle' | 'focus' | 'break';

export interface TimerConfig {
  focusMs: number;
  shortBreakMs: number;
  longBreakMs: number;
  cyclesBeforeLong: number;
}

export interface SessionRecord {
  id: number;
  userId?: string; // Owner of the session — used to filter per-user
  name?: string;
  mode: TimerMode;
  phase: 'focus' | 'break';
  cycle: number;
  durationMs: number;
  startedAt: Date;
  completedAt: Date;
  productivity: number; // 0-100, or -1 = not scored (< 5min)
}

// ============================================================================
// CONFIG
// ============================================================================

export const DEFAULT_CONFIGS: Record<TimerMode, TimerConfig> = {
  pomodoro: {
    focusMs: 25 * 60000,
    shortBreakMs: 5 * 60000,
    longBreakMs: 15 * 60000,
    cyclesBeforeLong: 4,
  },
  ultradian: {
    focusMs: 90 * 60000,
    shortBreakMs: 20 * 60000,
    longBreakMs: 20 * 60000,
    cyclesBeforeLong: 1,
  },
};

const TIMER_CONFIG_KEY = 'timetrack-timer-config';

/**
 * Read user's timer config from localStorage, merging with defaults.
 * Stored as JSON: { pomodoro: {...}, ultradian: {...} }
 */
export function getUserTimerConfig(mode: TimerMode): TimerConfig {
  try {
    const raw = localStorage.getItem(TIMER_CONFIG_KEY);
    if (raw) {
      const parsed = JSON.parse(raw);
      const modeConfig = parsed[mode];
      if (modeConfig) {
        return {
          focusMs: (modeConfig.focusMs ?? DEFAULT_CONFIGS[mode].focusMs),
          shortBreakMs: (modeConfig.shortBreakMs ?? DEFAULT_CONFIGS[mode].shortBreakMs),
          longBreakMs: (modeConfig.longBreakMs ?? DEFAULT_CONFIGS[mode].longBreakMs),
          cyclesBeforeLong: (modeConfig.cyclesBeforeLong ?? DEFAULT_CONFIGS[mode].cyclesBeforeLong),
        };
      }
    }
  } catch { /* fall through */ }
  return DEFAULT_CONFIGS[mode];
}

export function saveUserTimerConfig(mode: TimerMode, config: TimerConfig): void {
  try {
    const raw = localStorage.getItem(TIMER_CONFIG_KEY);
    const existing = raw ? JSON.parse(raw) : {};
    existing[mode] = config;
    localStorage.setItem(TIMER_CONFIG_KEY, JSON.stringify(existing));
  } catch { /* ignore */ }
}

/** @deprecated Use getUserTimerConfig instead */
export const CONFIGS = DEFAULT_CONFIGS;

export function getUltradianWaves(): number {
  try {
    return parseInt(localStorage.getItem('timetrack-ultradian-waves') ?? '1', 10) || 1;
  } catch {
    return 1;
  }
}

// ============================================================================
// HELPERS
// ============================================================================

/** Read the current user ID from the auth store's persisted localStorage */
function getCurrentUserId(): string | null {
  try {
    const raw = localStorage.getItem('timetrack-auth');
    if (!raw) return null;
    const parsed = JSON.parse(raw);
    return parsed?.state?.user?.id ?? null;
  } catch {
    return null;
  }
}

// ============================================================================
// INTERVAL MANAGEMENT (module-level, survives component unmounts)
// ============================================================================

let tickInterval: ReturnType<typeof setInterval> | null = null;

function ensureInterval() {
  if (tickInterval) return;
  tickInterval = setInterval(() => {
    useTimerStore.getState()._tick();
  }, 100);
}

function clearTickInterval() {
  if (tickInterval) {
    clearInterval(tickInterval);
    tickInterval = null;
  }
}

// ============================================================================
// STATE INTERFACE
// ============================================================================

interface TimerState {
  // Config
  mode: TimerMode;

  // Timer state
  phase: TimerPhase;
  remainingMs: number;
  totalMs: number;
  cycle: number;
  isPaused: boolean;
  ultradianWaves: number;

  // Timestamp tracking (wall-clock based)
  phaseStartedAt: number; // ms timestamp when current phase started
  pausedAt: number; // ms timestamp when paused (0 = not paused)
  pausedElapsedMs: number; // total ms spent paused during current phase

  // Session info
  sessionName: string;
  selectedProject: string;
  sessions: SessionRecord[];

  // Internal counter
  _sessionIdCounter: number;

  // Actions — public
  setMode: (mode: TimerMode) => void;
  start: () => void;
  togglePause: () => void;
  skip: () => void;
  stop: () => void;
  setSessionName: (name: string) => void;
  setSelectedProject: (id: string) => void;
  resetForLogout: () => void;

  // Actions — internal
  _tick: () => void;
  _handlePhaseComplete: () => void;
  _recordSession: (phase: 'focus' | 'break') => void;
}

// ============================================================================
// STORE
// ============================================================================

// Custom storage that handles Date serialization/deserialization
const timerStorage: PersistStorage<Partial<TimerState>> = {
  getItem: (name) => {
    const str = localStorage.getItem(name);
    if (!str) return null;
    const parsed = JSON.parse(str);
    // Convert date strings back to Date objects in sessions
    if (parsed?.state?.sessions) {
      parsed.state.sessions = parsed.state.sessions.map((s: Record<string, unknown>) => ({
        ...s,
        startedAt: new Date(s.startedAt as string),
        completedAt: new Date(s.completedAt as string),
      }));
    }
    return parsed;
  },
  setItem: (name, value) => localStorage.setItem(name, JSON.stringify(value)),
  removeItem: (name) => localStorage.removeItem(name),
};

export const useTimerStore = create<TimerState>()(
  subscribeWithSelector(
    persist(
      (set, get) => ({
    mode: 'pomodoro' as TimerMode,
    phase: 'idle' as TimerPhase,
    remainingMs: getUserTimerConfig('pomodoro').focusMs,
    totalMs: getUserTimerConfig('pomodoro').focusMs,
    cycle: 0,
    isPaused: false,
    ultradianWaves: getUltradianWaves(),
    phaseStartedAt: 0,
    pausedAt: 0,
    pausedElapsedMs: 0,
    sessionName: '',
    selectedProject: '',
    sessions: [],
    _sessionIdCounter: 0,

    // ------------------------------------------------------------------
    // PUBLIC ACTIONS
    // ------------------------------------------------------------------

    setMode: (mode) => {
      if (get().phase !== 'idle') return;
      const config = getUserTimerConfig(mode);
      set({
        mode,
        remainingMs: config.focusMs,
        totalMs: config.focusMs,
        cycle: 0,
      });
    },

    start: () => {
      const { mode } = get();
      const config = getUserTimerConfig(mode);
      const waves = getUltradianWaves();
      set({
        phase: 'focus',
        cycle: 0,
        totalMs: config.focusMs,
        remainingMs: config.focusMs,
        phaseStartedAt: Date.now(),
        pausedAt: 0,
        pausedElapsedMs: 0,
        isPaused: false,
        ultradianWaves: waves,
      });
      ensureInterval();
    },

    togglePause: () => {
      const state = get();
      if (state.phase === 'idle') return;

      if (state.isPaused) {
        // Resume — accumulate time spent paused
        const pauseDuration = Date.now() - state.pausedAt;
        set({
          isPaused: false,
          pausedAt: 0,
          pausedElapsedMs: state.pausedElapsedMs + pauseDuration,
        });
      } else {
        // Pause
        set({
          isPaused: true,
          pausedAt: Date.now(),
        });
      }
    },

    skip: () => {
      get()._handlePhaseComplete();
    },

    stop: () => {
      const state = get();
      if (state.phase !== 'idle') {
        const effectiveNow = state.isPaused ? state.pausedAt : Date.now();
        const elapsed = effectiveNow - state.phaseStartedAt - state.pausedElapsedMs;
        if (elapsed > 5000) {
          get()._recordSession(state.phase as 'focus' | 'break');
        }
      }
      const config = getUserTimerConfig(get().mode);
      set({
        phase: 'idle',
        isPaused: false,
        pausedAt: 0,
        pausedElapsedMs: 0,
        cycle: 0,
        remainingMs: config.focusMs,
        totalMs: config.focusMs,
        phaseStartedAt: 0,
      });
      clearTickInterval();
    },

    setSessionName: (name) => set({ sessionName: name }),
    setSelectedProject: (id) => set({ selectedProject: id }),

    resetForLogout: () => {
      // Stop any running timer, but keep sessions — they are tagged by userId
      // and filtered per-user via selectCurrentUserSessions.
      clearTickInterval();
      const config = getUserTimerConfig(get().mode);
      set({
        phase: 'idle',
        isPaused: false,
        pausedAt: 0,
        pausedElapsedMs: 0,
        cycle: 0,
        remainingMs: config.focusMs,
        totalMs: config.focusMs,
        phaseStartedAt: 0,
        sessionName: '',
        selectedProject: '',
      });
    },

    // ------------------------------------------------------------------
    // INTERNAL ACTIONS
    // ------------------------------------------------------------------

    _tick: () => {
      const state = get();
      if (state.phase === 'idle' || state.isPaused) return;

      const now = Date.now();
      const elapsed = now - state.phaseStartedAt - state.pausedElapsedMs;
      const remaining = Math.max(0, state.totalMs - elapsed);

      if (remaining <= 0) {
        get()._handlePhaseComplete();
      } else {
        set({ remainingMs: remaining });
      }
    },

    _handlePhaseComplete: () => {
      const state = get();
      const config = getUserTimerConfig(state.mode);

      // Record the completed phase
      get()._recordSession(state.phase as 'focus' | 'break');

      if (state.phase === 'focus') {
        const newCycle = state.cycle + 1;

        // Ultradian: check if all waves are done
        if (state.mode === 'ultradian' && newCycle >= state.ultradianWaves) {
          set({
            phase: 'idle',
            isPaused: false,
            pausedAt: 0,
            pausedElapsedMs: 0,
            cycle: 0,
            remainingMs: config.focusMs,
            totalMs: config.focusMs,
            phaseStartedAt: 0,
          });
          clearTickInterval();
          return;
        }

        const isLong = newCycle % config.cyclesBeforeLong === 0;
        const breakMs = isLong ? config.longBreakMs : config.shortBreakMs;
        set({
          phase: 'break',
          cycle: newCycle,
          totalMs: breakMs,
          remainingMs: breakMs,
          phaseStartedAt: Date.now(),
          pausedAt: 0,
          pausedElapsedMs: 0,
        });
      } else if (state.phase === 'break') {
        set({
          phase: 'focus',
          totalMs: config.focusMs,
          remainingMs: config.focusMs,
          phaseStartedAt: Date.now(),
          pausedAt: 0,
          pausedElapsedMs: 0,
        });
      }
    },

    _recordSession: (ph) => {
      const state = get();
      const config = getUserTimerConfig(state.mode);
      const effectiveNow = state.isPaused ? state.pausedAt : Date.now();
      const durationMs = effectiveNow - state.phaseStartedAt - state.pausedElapsedMs;

      const tooShort = ph === 'focus' && durationMs < 5 * 60000;
      const score = tooShort
        ? -1
        : ph === 'focus'
          ? Math.min(100, Math.round(70 + (durationMs / config.focusMs) * 30))
          : 50;

      const completedAt = new Date(effectiveNow);
      const startedAt = new Date(state.phaseStartedAt);

      const sessionCycle = state.cycle + (ph === 'focus' ? 1 : 0);
      const sessionName = state.sessionName || undefined;
      const sessionMode = state.mode;

      set((prev) => ({
        _sessionIdCounter: prev._sessionIdCounter + 1,
        sessions: [
          {
            id: prev._sessionIdCounter + 1,
            userId: getCurrentUserId() ?? undefined,
            name: sessionName,
            mode: sessionMode,
            phase: ph,
            cycle: sessionCycle,
            durationMs,
            startedAt,
            completedAt,
            productivity: score,
          },
          ...prev.sessions,
        ],
      }));

      // Sync focus sessions to backend (fire-and-forget)
      if (ph === 'focus') {
        try {
          const ipc = getIpcService();
          ipc.sendCommand('recordFocusSession', {
            id: crypto.randomUUID(),
            startedAt: startedAt.toISOString(),
            completedAt: completedAt.toISOString(),
            durationMs,
            mode: sessionMode,
            cycle: sessionCycle,
            name: sessionName,
            productivity: score,
          });
        } catch {
          // Silent — localStorage is the primary store, cloud sync is best-effort
        }
      }
    },
  }),
      {
        name: 'xchronus-timer-store',
        storage: timerStorage,
        // Only persist sessions and ID counter — timer state resets on restart
        partialize: (state) => ({
          sessions: state.sessions,
          _sessionIdCounter: state._sessionIdCounter,
        }),
      },
    ),
  ),
);

// ============================================================================
// SELECTORS
// ============================================================================

export const selectTimerPhase = (s: TimerState) => s.phase;
export const selectTimerMode = (s: TimerState) => s.mode;
export const selectTimerSessions = (s: TimerState) => s.sessions;

/** Returns only the sessions belonging to the currently logged-in user */
export function selectCurrentUserSessions(s: TimerState): SessionRecord[] {
  const uid = getCurrentUserId();
  if (!uid) return [];
  return s.sessions.filter((sess) => sess.userId === uid);
}

// ============================================================================
// VISIBILITY CHANGE — recalculate immediately when app regains focus
// ============================================================================

if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', () => {
    if (document.visibilityState === 'visible') {
      const state = useTimerStore.getState();
      if (state.phase !== 'idle' && !state.isPaused) {
        state._tick();
        ensureInterval(); // re-ensure in case browser killed it
      }
    }
  });
}
