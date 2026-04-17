type ApiClient = {
  get: <T = unknown>(_path: string) => Promise<T>;
  post: <T = unknown>(_path: string, _body?: unknown) => Promise<T>;
  put: <T = unknown>(_path: string, _body?: unknown) => Promise<T>;
  patch: <T = unknown>(_path: string, _body?: unknown) => Promise<T>;
  delete: <T = unknown>(_path: string) => Promise<T>;
};

export const api: ApiClient = {
  async get() {
    throw new Error('Demo mode: apiClient.get disabled');
  },
  async post() {
    throw new Error('Demo mode: apiClient.post disabled');
  },
  async put() {
    throw new Error('Demo mode: apiClient.put disabled');
  },
  async patch() {
    throw new Error('Demo mode: apiClient.patch disabled');
  },
  async delete() {
    throw new Error('Demo mode: apiClient.delete disabled');
  },
};

