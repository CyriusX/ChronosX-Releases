/**
 * Auth Store - Web Admin Portal version
 *
 * Same as desktop authStore but:
 * - No IPC token sync (no agent in browser)
 * - No trackingStore/timerStore resets on logout
 * - Rejects Colaborador role on login (admin portal only)
 * - Different localStorage key to avoid collision
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

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
  onboardingCompleted?: boolean;
}

interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
}

interface AuthState {
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  error: string | null;

  login: (email: string, password: string) => Promise<boolean>;
  register: (email: string, password: string, displayName: string, orgName: string) => Promise<boolean>;
  logout: () => Promise<void>;
  refreshTokens: () => Promise<boolean>;
  setUser: (user: User) => void;
  setTokens: (tokens: AuthTokens) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  setOnboardingComplete: (complete: boolean) => void;
  clearAuth: () => void;
}

// ============================================================================
// API HELPERS
// ============================================================================

// Get API URL from runtime config (injected by config.js at container startup)
const getApiBaseUrl = (): string => {
  let url: string | undefined;
  // Check for runtime config (injected by config.js)
  if (typeof window !== 'undefined' && window.__APP_CONFIG__?.VITE_API_URL) {
    url = window.__APP_CONFIG__.VITE_API_URL;
  }
  // Fallback to build-time env var (for development)
  if (!url) {
    url = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';
  }
  return url.replace(/\/+$/, '');
};

const API_BASE = getApiBaseUrl();

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
  const response = await fetch(`${API_BASE}/auth/refresh`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  });

  if (!response.ok) {
    throw new Error('Token refresh failed');
  }

  return response.json();
}

async function registerApi(email: string, password: string, displayName: string, orgName: string) {
  const response = await fetch(`${API_BASE}/auth/register`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, displayName, organizationName: orgName }),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Registration failed' }));
    throw new Error(error.message || error.title || 'Registration failed');
  }

  return response.json();
}

async function getOnboardingStatusApi(accessToken: string): Promise<boolean> {
  try {
    const response = await fetch(`${API_BASE}/onboarding/status`, {
      headers: { Authorization: `Bearer ${accessToken}` },
    });
    if (!response.ok) return false;
    const data = await response.json();
    return data.isComplete ?? false;
  } catch {
    return false;
  }
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
      user: null,
      tokens: null,
      isAuthenticated: false,
      isLoading: false,
      error: null,

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

          // Reject Colaborador role — admin portal only
          if (user.role === 'Colaborador') {
            set({
              user: null,
              tokens: null,
              isAuthenticated: false,
              isLoading: false,
              error: 'Acesso restrito a gestores e administradores. Use o aplicativo desktop para acessar como colaborador.',
            });
            return false;
          }

          const tokens: AuthTokens = {
            accessToken: response.accessToken,
            refreshToken: response.refreshToken,
            expiresAt: Date.now() + response.expiresIn * 1000,
          };

          const onboardingCompleted = await getOnboardingStatusApi(tokens.accessToken);
          user.onboardingCompleted = onboardingCompleted;

          set({
            user,
            tokens,
            isAuthenticated: true,
            isLoading: false,
            error: null,
          });

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

      logout: async () => {
        const { tokens } = get();

        if (tokens?.refreshToken) {
          await logoutApi(tokens.refreshToken);
        }

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

      register: async (email: string, password: string, displayName: string, orgName: string) => {
        set({ isLoading: true, error: null });
        try {
          await registerApi(email, password, displayName, orgName);
          // Auto-login after registration
          const loginResponse = await loginApi(email, password);
          const user: User = {
            id: loginResponse.userId,
            email: loginResponse.email ?? email,
            displayName: loginResponse.displayName,
            role: loginResponse.role ?? 'Colaborador',
            orgId: loginResponse.orgId,
            orgName: loginResponse.orgName,
            passwordMustChange: loginResponse.passwordMustChange ?? false,
          };
          const tokens: AuthTokens = {
            accessToken: loginResponse.accessToken,
            refreshToken: loginResponse.refreshToken,
            expiresAt: Date.now() + loginResponse.expiresIn * 1000,
          };
          const onboardingCompleted = await getOnboardingStatusApi(tokens.accessToken);
          user.onboardingCompleted = onboardingCompleted;
          set({ user, tokens, isAuthenticated: true, isLoading: false, error: null });
          return true;
        } catch (err) {
          const message = err instanceof Error ? err.message : 'Registration failed';
          set({ isLoading: false, error: message });
          return false;
        }
      },

      setUser: (user) => set({ user, isAuthenticated: true }),
      setTokens: (tokens) => set({ tokens }),
      setLoading: (isLoading) => set({ isLoading }),
      setError: (error) => set({ error }),
      setOnboardingComplete: (complete: boolean) =>
        set((state) => ({
          user: state.user ? { ...state.user, onboardingCompleted: complete } : null,
        })),

      clearAuth: () =>
        set({
          user: null,
          tokens: null,
          isAuthenticated: false,
          error: null,
        }),
    }),
    {
      name: 'timetrack-web-auth',
      partialize: (state): PersistedAuth => ({
        user: state.user,
        tokens: state.tokens,
      }),
      onRehydrateStorage: () => (state) => {
        if (state?.tokens && state.tokens.expiresAt < Date.now()) {
          state.clearAuth();
        } else if (state?.user && state?.tokens) {
          // Also reject if somehow a Colaborador was persisted
          if (state.user.role === 'Colaborador') {
            state.clearAuth();
          } else {
            state.isAuthenticated = true;
          }
        }
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
