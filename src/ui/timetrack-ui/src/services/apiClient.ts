/**
 * API Client - Centralized HTTP client with 401 handling and auto-refresh
 *
 * Features:
 * - Automatic token refresh on 401
 * - Request queueing during refresh (prevents multiple refresh calls)
 * - Logout redirect on refresh failure
 * - Consistent error handling
 * - Session expired event dispatch
 */

import { useAuthStore } from '../stores/authStore';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api/v1';

// ============================================================================
// TYPES
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

// ============================================================================
// SESSION EXPIRED EVENT
// ============================================================================

export const SESSION_EXPIRED_EVENT = 'timetrack:session-expired';

export function dispatchSessionExpired(reason: string = 'Sessão expirada') {
  window.dispatchEvent(new CustomEvent(SESSION_EXPIRED_EVENT, { detail: { reason } }));
}

// ============================================================================
// REFRESH MANAGEMENT
// ============================================================================

let isRefreshing = false;
let refreshPromise: Promise<boolean> | null = null;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: Error) => void;
}> = [];

async function refreshTokens(): Promise<boolean> {
  const authStore = useAuthStore.getState();

  if (!authStore.tokens?.refreshToken) {
    return false;
  }

  try {
    const response = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: authStore.tokens.refreshToken }),
    });

    if (!response.ok) {
      return false;
    }

    const data = await response.json();

    authStore.setTokens({
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      expiresAt: Date.now() + data.expiresIn * 1000,
    });

    return true;
  } catch {
    return false;
  }
}

async function handleTokenRefresh(): Promise<string | null> {
  // If already refreshing, queue this request
  if (isRefreshing && refreshPromise) {
    return new Promise((resolve, reject) => {
      failedQueue.push({
        resolve: (token) => resolve(token),
        reject: (error) => reject(error),
      });
    });
  }

  isRefreshing = true;
  refreshPromise = refreshTokens();

  const success = await refreshPromise;

  isRefreshing = false;
  refreshPromise = null;

  if (success) {
    const newToken = useAuthStore.getState().tokens?.accessToken;
    // Process queued requests
    failedQueue.forEach(({ resolve }) => {
      if (newToken) resolve(newToken);
    });
    failedQueue = [];
    return newToken || null;
  } else {
    // Refresh failed - logout user
    const error = new Error('Session expired');
    failedQueue.forEach(({ reject }) => reject(error));
    failedQueue = [];

    // Dispatch session expired event before redirect
    dispatchSessionExpired('Sua sessão expirou. Por favor, faça login novamente.');

    // Clear auth and redirect to login
    useAuthStore.getState().clearAuth();
    window.location.href = '/login';

    return null;
  }
}

// ============================================================================
// API CLIENT
// ============================================================================

/**
 * Centralized API client with automatic 401 handling
 */
export async function apiClient<T>(
  endpoint: string,
  options: ApiClientOptions = {}
): Promise<T> {
  const { method = 'GET', body, headers = {}, skipAuth = false } = options;

  const authStore = useAuthStore.getState();
  let accessToken = authStore.tokens?.accessToken;

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

  // Make request
  const makeRequest = async (token?: string): Promise<Response> => {
    if (token) {
      requestHeaders['Authorization'] = `Bearer ${token}`;
    }

    return fetch(url, {
      method,
      headers: requestHeaders,
      body: body ? JSON.stringify(body) : undefined,
    });
  };

  let response = await makeRequest(accessToken);

  // Handle 401 - try refresh
  if (response.status === 401 && !skipAuth) {
    const newToken = await handleTokenRefresh();

    if (newToken) {
      // Retry with new token
      response = await makeRequest(newToken);
    } else {
      // Refresh failed, throw error (already handled redirect)
      throw new Error('Session expired. Please login again.');
    }
  }

  // Handle response
  if (!response.ok) {
    let errorMessage = `HTTP ${response.status}`;
    let errorCode: string | undefined;

    try {
      const errorData = await response.json();
      errorMessage = errorData.message
        || errorData.title
        || errorData.error
        || (typeof errorData === 'string' ? errorData : null)
        || errorMessage;
      errorCode = errorData.code;
    } catch {
      // Failed to parse error response
    }

    const error = new Error(errorMessage) as unknown as ApiError;
    error.status = response.status;
    error.code = errorCode;
    throw error;
  }

  // Handle 204 No Content
  if (response.status === 204) {
    return undefined as T;
  }

  return response.json();
}

// ============================================================================
// CONVENIENCE METHODS
// ============================================================================

export const api = {
  get: <T>(endpoint: string, options?: Omit<ApiClientOptions, 'method' | 'body'>) =>
    apiClient<T>(endpoint, { ...options, method: 'GET' }),

  post: <T>(endpoint: string, body?: unknown, options?: Omit<ApiClientOptions, 'method' | 'body'>) =>
    apiClient<T>(endpoint, { ...options, method: 'POST', body }),

  put: <T>(endpoint: string, body?: unknown, options?: Omit<ApiClientOptions, 'method' | 'body'>) =>
    apiClient<T>(endpoint, { ...options, method: 'PUT', body }),

  patch: <T>(endpoint: string, body?: unknown, options?: Omit<ApiClientOptions, 'method' | 'body'>) =>
    apiClient<T>(endpoint, { ...options, method: 'PATCH', body }),

  delete: <T>(endpoint: string, options?: Omit<ApiClientOptions, 'method'>) =>
    apiClient<T>(endpoint, { ...options, method: 'DELETE' }),
};

export type { ApiError };
