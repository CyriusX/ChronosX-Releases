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
let refreshPromise: Promise<'success' | 'auth_failed' | 'network_error'> | null = null;
let failedQueue: Array<{
  resolve: (token: string) => void;
  reject: (error: Error) => void;
}> = [];

async function refreshTokens(): Promise<'success' | 'auth_failed' | 'network_error'> {
  const authStore = useAuthStore.getState();

  if (!authStore.tokens?.refreshToken) {
    return 'auth_failed';
  }

  try {
    const response = await fetch(`${API_BASE}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refreshToken: authStore.tokens.refreshToken }),
    });

    if (!response.ok) {
      // Server explicitly rejected the refresh token (expired, revoked, invalid)
      return 'auth_failed';
    }

    const data = await response.json();

    authStore.setTokens({
      accessToken: data.accessToken,
      refreshToken: data.refreshToken,
      expiresAt: Date.now() + data.expiresIn * 1000,
    });

    return 'success';
  } catch {
    // Network error (offline, DNS failure, timeout) — don't clear auth
    return 'network_error';
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

  const result = await refreshPromise;

  isRefreshing = false;
  refreshPromise = null;

  if (result === 'success') {
    const newToken = useAuthStore.getState().tokens?.accessToken;
    failedQueue.forEach(({ resolve }) => {
      if (newToken) resolve(newToken);
    });
    failedQueue = [];
    return newToken || null;
  } else if (result === 'network_error') {
    // Network error — don't clear auth. The user is still authenticated,
    // they just can't reach the server right now.
    const error = new Error('Network error');
    failedQueue.forEach(({ reject }) => reject(error));
    failedQueue = [];
    return null;
  } else {
    // Auth failed (refresh token expired/revoked) — logout user
    const error = new Error('Session expired');
    failedQueue.forEach(({ reject }) => reject(error));
    failedQueue = [];
    dispatchSessionExpired('Sua sessão expirou. Por favor, faça login novamente.');
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

  const sleep = (ms: number) => new Promise<void>((resolve) => setTimeout(resolve, ms));
  const getRetryAfterMs = (response: Response) => {
    const raw = response.headers.get('Retry-After');
    if (!raw) return 1000;
    const asSeconds = Number(raw);
    if (!Number.isNaN(asSeconds)) return Math.max(0, asSeconds) * 1000;
    const asDate = Date.parse(raw);
    if (!Number.isNaN(asDate)) return Math.max(0, asDate - Date.now());
    return 1000;
  };

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

  const max429Retries = 3;
  let attempt429 = 0;
  let response: Response;

  while (true) {
    response = await makeRequest(accessToken);

    // Handle 401 - try refresh
    if (response.status === 401 && !skipAuth) {
      const newToken = await handleTokenRefresh();

      if (newToken) {
        accessToken = newToken;
        // Retry with new token
        response = await makeRequest(newToken);
      } else if (useAuthStore.getState().isAuthenticated) {
        // Refresh failed due to network error — auth state is preserved, just throw
        throw new Error('Network error. Please check your connection and try again.');
      } else {
        // Refresh failed due to auth error — already redirected to login
        throw new Error('Session expired. Please login again.');
      }
    }

    // Handle 429 (rate limiting) with bounded backoff; GET-only to avoid double-posting.
    if (response.status === 429 && method === 'GET' && attempt429 < max429Retries) {
      attempt429 += 1;
      const delayMs = Math.min(getRetryAfterMs(response), 30000);
      console.warn(`[apiClient] 429 Rate limit. Retrying in ${delayMs}ms (attempt ${attempt429}/${max429Retries})`, {
        endpoint: url,
      });
      await sleep(delayMs);
      continue;
    }

    break;
  }

  // Handle response
  if (!response.ok) {
    let errorMessage = `HTTP ${response.status}`;
    let errorCode: string | undefined;

    try {
      const errorData = await response.json();
      console.error('[apiClient] Error response body:', JSON.stringify(errorData, null, 2));
      errorMessage = errorData.message
        || errorData.title
        || errorData.error
        || (typeof errorData === 'string' ? errorData : null)
        || errorMessage;
      errorCode = errorData.code;

      // Include validation errors in the message for easier debugging
      if (errorData.errors && typeof errorData.errors === 'object') {
        const details = Object.entries(errorData.errors)
          .map(([k, v]) => `${k}: ${(v as string[]).join(', ')}`)
          .join('; ');
        errorMessage = details || errorMessage;
      }
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
