type ApiClientOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  body?: unknown;
  headers?: Record<string, string>;
  skipAuth?: boolean;
};

export type ApiError = {
  message: string;
  status: number;
  code?: string;
};

export const SESSION_EXPIRED_EVENT = 'timetrack:session-expired';

export function dispatchSessionExpired(_reason: string = 'Session expired') {
  // Demo mode: no-op (no auth refresh logic / redirects)
}

export async function apiClient<T>(_endpoint: string, _options: ApiClientOptions = {}): Promise<T> {
  throw new Error('Demo mode: apiClient disabled (no network)');
}

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
