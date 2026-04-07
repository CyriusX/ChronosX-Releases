/**
 * API Client - Web Admin Portal version
 *
 * Same as desktop apiClient but imports from web authStore.
 * This is necessary because the desktop apiClient imports its own authStore
 * via relative path, which would use a different localStorage key.
 */

import { useAuthStore } from '../stores/authStore';

// Get API URL from runtime config (set by config.js) or fallback to default
const getApiBaseUrl = () => {
  // Check for runtime config (injected by config.js)
  if (typeof window !== 'undefined' && window.__APP_CONFIG__?.VITE_API_URL) {
    return window.__APP_CONFIG__.VITE_API_URL;
  }
  // Fallback to build-time env var (for development)
  return import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';
};

const API_BASE = getApiBaseUrl();

// ============================================================================// TYPES
// ============================================================================

interface ApiClientOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  body?: unknown;
  headers?: Record<string, string>;
  skipAuth?: boolean;
}

interface ApiError {
  message: string;
  status: number;
  code?: string;
}

// ============================================================================// SESSION EXPIRED EVENT
// ============================================================================

export const SESSION_EXPIRED_EVENT = 'timetrack:session-expired';

export function dispatchSessionExpired(reason: string = 'Sessao expirada') {
  window.dispatchEvent(new CustomEvent(SESSION_EXPIRED_EVENT, { detail: { reason } }));
}

// ============================================================================// REFRESH MANAGEMENT
// ============================================================================

let isRefreshing = false;
let refreshPromise: Promise<boolean> | null = null;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: Error) => void;
}> = [];

async function refreshAccessToken(): Promise<boolean> {
  if (isRefreshing && refreshPromise) {
    return refreshPromise;
  }

  const { tokens } = useAuthStore.getState();
  if (!tokens?.refreshToken) {
    return false;
  }

  isRefreshing = true;
  refreshPromise = (async () => {
    try {
      const response = await fetch(`${API_BASE}/auth/refresh`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ refreshToken: tokens.refreshToken }),
      });

      if (!response.ok) {
        // Refresh token is invalid - clear auth and redirect to login
        useAuthStore.getState().clearAuth();
        dispatchSessionExpired('Token de atualização inválido');
        return false;
      }

      const data = await response.json();
      useAuthStore.getState().setTokens({
        accessToken: data.accessToken,
        refreshToken: data.refreshToken || tokens.refreshToken,
      });

      // Process any failed requests that were waiting
      failedQueue.forEach(({ resolve }) => resolve(data.accessToken));
      failedQueue = [];

      return true;
    } catch (error) {
      console.error('Failed to refresh token:', error);
      useAuthStore.getState().clearAuth();
      dispatchSessionExpired('Falha ao atualizar token');
      return false;
    } finally {
      isRefreshing = false;
      refreshPromise = null;
    }
  })();

  return refreshPromise;
}

// ============================================================================// API CLIENT
// ============================================================================

export async function apiClient<T>(
  endpoint: string,
  options: ApiClientOptions = {}
): Promise<T> {
  const { method = 'GET', body, headers = {}, skipAuth = false } = options;

  // Get fresh token from authStore
  const { tokens } = useAuthStore.getState();
  const accessToken = tokens?.accessToken;

  // Build headers
  const requestHeaders: Record<string, string> = {
    'Content-Type': 'application/json',
    ...headers,
  };

  if (!skipAuth && accessToken) {
    requestHeaders['Authorization'] = `Bearer ${accessToken}`;
  }

  // Build URL
  const url = endpoint.startsWith('http') ? endpoint : `${API_BASE}${endpoint}`;

  try {
    const response = await fetch(url, {
      method,
      headers: requestHeaders,
      body: body ? JSON.stringify(body) : undefined,
    });

    // Handle 401 - try to refresh token
    if (response.status === 401 && !skipAuth) {
      const refreshed = await refreshAccessToken();
      if (refreshed) {
        // Retry with new token
        const newTokens = useAuthStore.getState().tokens;
        if (newTokens?.accessToken) {
          requestHeaders['Authorization'] = `Bearer ${newTokens.accessToken}`;
          const retryResponse = await fetch(url, {
            method,
            headers: requestHeaders,
            body: body ? JSON.stringify(body) : undefined,
          });
          if (retryResponse.ok) {
            return retryResponse.json();
          }
        }
      }
      // Refresh failed - clear auth
      useAuthStore.getState().clearAuth();
      dispatchSessionExpired('Sessão expirada');
      throw new Error('Sessão expirada');
    }

    if (!response.ok) {
      const errorData = await response.json().catch(() => ({}));
      const error: ApiError = {
        message: errorData.message || errorData.error || `HTTP error ${response.status}`,
        status: response.status,
        code: errorData.code,
      };
      throw error;
    }

    // Handle 204 No Content
    if (response.status === 204) {
      return undefined as T;
    }

    return response.json();
  } catch (error) {
    console.error('API Error:', error);
    throw error;
  }
}
