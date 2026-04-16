/**
 * Auth Store - Zustand state management for authentication
 *
 * Manages user session, tokens, and authentication state
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import { globalEventDispatcher } from '../services/eventDispatcher';

// ============================================================================
// TYPES
// ============================================================================

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: 'Colaborador' | 'Gestor' | 'Admin';
  orgId: string;
  orgName: string;
  passwordMustChange: boolean;
}

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number; // Unix timestamp
}

interface RegisterResponse {
  userId: string;
  orgId: string;
  email: string;
  displayName: string;
  organizationName: string;
}

interface AuthState {
  // State
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isRehydrating: boolean;
  error: string | null;

  // Actions
  login: (email: string, password: string) => Promise<boolean>;
  register: (email: string, password: string, displayName: string, organizationName: string) => Promise<RegisterResponse | null>;
  logout: () => Promise<void>;
  refreshTokens: () => Promise<boolean>;
  setUser: (user: User) => void;
  setTokens: (tokens: AuthTokens) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  clearAuth: () => void;
}

// ============================================================================
// API HELPERS
// ============================================================================

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

async function loginApi(email: string, password: string) {
  const response = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Login failed' }));
    throw new Error(error.message || error.title || 'Login failed');
  }

  return response.json();
}

async function refreshApi(refreshToken: string) {
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), 10000); // 10s timeout

  try {
    const response = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
      signal: controller.signal,
    });

    if (!response.ok) {
      throw new Error('Token refresh failed');
    }

    return response.json();
  } finally {
    clearTimeout(timeout);
  }
}

async function registerApi(email: string, password: string, displayName: string, organizationName: string): Promise<RegisterResponse> {
  const response = await fetch(`${API_BASE}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, displayName, organizationName }),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Registration failed' }));
    if (error.errors) {
      const messages = Object.values(error.errors as Record<string, string[]>).flat();
      throw new Error(messages.join('; '));
    }
    throw new Error(error.message || error.title || 'Registration failed');
  }

  return response.json();
}

async function logoutApi(refreshToken?: string) {
  try {
    await fetch(`${API_BASE}/auth/logout`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken }),
    });
  } catch {
    // Ignore logout errors
  }
}

// ============================================================================
// STORE
// ============================================================================

interface PersistedAuth {
  user: User | null;
  tokens: AuthTokens | null;
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      // Initial state
      user: null,
      tokens: null,
      isAuthenticated: false,
      isLoading: false,
      isRehydrating: true, // true until rehydration completes
      error: null,

      // Actions
      login: async (email: string, password: string) => {
        set({ isLoading: true, error: null });

        try {
          const response = await loginApi(email, password);

          const user: User = {
            id: response.userId,
            email: response.email ?? email,
            displayName: response.displayName,
            role: response.role ?? 'Colaborador',
            orgId: response.orgId,
            orgName: response.orgName,
            passwordMustChange: response.passwordMustChange ?? false,
          };

          const tokens: AuthTokens = {
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            expiresAt: Date.now() + response.expiresIn * 1000,
          };

          set({
            user,
            tokens,
            isAuthenticated: true,
            isLoading: false,
            error: null,
          });

          // Notify Agent about the new tokens
          try {
            const { getIpcService } = await import('../services');
            const ipcService = getIpcService();
            await ipcService.sendCommand('storeTokens', {
              accessToken: response.accessToken,
              refreshToken: response.refreshToken
            });
            console.log('[AuthStore] Tokens sent to Agent');
          } catch (ipcError) {
            console.warn('[AuthStore] Failed to send tokens to Agent:', ipcError);
          }

          return true;
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Login failed';
          set({
            user: null,
            tokens: null,
            isAuthenticated: false,
            isLoading: false,
            error: message,
          });
          return false;
        }
      },

      register: async (email: string, password: string, displayName: string, organizationName: string) => {
        set({ isLoading: true, error: null });

        try {
          const response = await registerApi(email, password, displayName, organizationName);

          set({ isLoading: false, error: null });

          return response;
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Registration failed';
          set({
            isLoading: false,
            error: message,
          });
          return null;
        }
      },

      logout: async () => {
        const { tokens } = get();

        if (tokens?.refreshToken) {
          await logoutApi(tokens.refreshToken);
        }

        // Reset tracking store to clear user-specific data
        const { useTrackingStore } = await import('./trackingStore');
        useTrackingStore.getState().reset();

        // Reset timer store to clear focus sessions from previous user
        const { useTimerStore } = await import('./timerStore');
        useTimerStore.getState().resetForLogout();

        set({
          user: null,
          tokens: null,
          isAuthenticated: false,
          error: null,
        });
      },

      refreshTokens: async () => {
        const { tokens } = get();

        if (!tokens?.refreshToken) {
          return false;
        }

        try {
          const response = await refreshApi(tokens.refreshToken);

          const newTokens: AuthTokens = {
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            expiresAt: Date.now() + response.expiresIn * 1000,
          };

          set({ tokens: newTokens });
          return true;
        } catch {
          set({
            user: null,
            tokens: null,
            isAuthenticated: false,
          });
          return false;
        }
      },

      setUser: (user) => set({ user, isAuthenticated: true }),

      setTokens: (tokens) => set({ tokens }),

      setLoading: (isLoading) => set({ isLoading }),

      setError: (error) => set({ error }),

      clearAuth: () => {
        set({
          user: null,
          tokens: null,
          isAuthenticated: false,
          error: null,
        });
      },
    }),
    {
      name: 'timetrack-auth',
      partialize: (state): PersistedAuth => ({
        user: state.user,
        tokens: state.tokens,
      }),
      // Use synchronous merge to restore session immediately.
      // This is more reliable than onRehydrateStorage which may not fire
      // in certain WebView2/Zustand timing scenarios.
      merge: (persistedState, currentState) => {
        const persisted = persistedState as Partial<PersistedAuth>;
        const hasSession = !!(persisted.tokens && persisted.user);

        return {
          ...currentState,
          ...persisted,
          isAuthenticated: hasSession,
          isRehydrating: false,
        };
      },
    }
  )
);

// ============================================================================
// SELECTORS
// ============================================================================

export const selectUser = (state: AuthState) => state.user;
export const selectIsAuthenticated = (state: AuthState) => state.isAuthenticated;
export const selectIsLoading = (state: AuthState) => state.isLoading;
export const selectError = (state: AuthState) => state.error;
export const selectAccessToken = (state: AuthState) => state.tokens?.accessToken;

// ============================================================================
// AGENT TOKEN SYNC
// ============================================================================

// When the Agent (background service) refreshes tokens, it broadcasts a
// tokensRefreshed IPC event. Subscribe here so the UI's localStorage always
// holds the latest valid refresh token — preventing "refresh token expired or
// revoked" errors on the next app start.
globalEventDispatcher.subscribe('tokensRefreshed', async (payload) => {
  const store = useAuthStore.getState();

  const newTokens = {
    accessToken: payload.accessToken,
    refreshToken: payload.refreshToken,
    expiresAt: Date.now() + payload.expiresIn * 1000,
  };

  // Always store the tokens (they're guaranteed valid — the Agent just refreshed them)
  store.setTokens(newTokens);

  if (!store.user) {
    // Session was cleared (e.g. UI's own refresh failed) but Agent has valid tokens.
    // Use the new access token to fetch user info and restore the session.
    try {
      const response = await fetch(`${API_BASE}/auth/me/summary`, {
        headers: { Authorization: `Bearer ${payload.accessToken}` },
      });
      if (response.ok) {
        const data = await response.json();
        store.setUser({
          id: data.userId ?? data.id,
          email: data.email ?? '',
          displayName: data.displayName ?? '',
          role: data.role ?? 'Colaborador',
          orgId: data.orgId ?? data.organizationId ?? '',
          orgName: data.orgName ?? data.organizationName ?? '',
          passwordMustChange: data.passwordMustChange ?? false,
        });
        console.log('[AuthStore] Session restored from Agent tokens');
        window.location.hash = '/';
      }
    } catch {
      // Can't reach backend — user will need to login manually
    }
    return;
  }

  console.log('[AuthStore] Tokens synced from Agent refresh');
});
