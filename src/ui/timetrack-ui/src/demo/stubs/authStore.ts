import { create } from 'zustand';

export interface User {
  id: string;
  email: string;
  displayName: string;
  role: 'Colaborador' | 'Gestor' | 'Admin';
  orgId: string;
  orgName: string;
  passwordMustChange: boolean;
}

type AuthTokens = {
  accessToken: string;
  refreshToken: string;
  expiresAt: number;
};

type AuthState = {
  user: User | null;
  tokens: AuthTokens | null;
  isAuthenticated: boolean;
  isLoading: boolean;
  isRehydrating: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<boolean>;
  register: (...args: any[]) => Promise<any>;
  logout: () => Promise<void>;
  refreshTokens: () => Promise<boolean>;
  setUser: (user: User) => void;
  setTokens: (tokens: AuthTokens) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  clearAuth: () => void;
};

function getAudience(): 'individuals' | 'teams' {
  try {
    const url = new URL(window.location.href);
    const a = url.searchParams.get('audience');
    if (a === 'teams') return 'teams';
  } catch {
    // ignore
  }
  return 'individuals';
}

function initialUser(): User {
  const audience = getAudience();
  const role: User['role'] = audience === 'teams' ? 'Gestor' : 'Colaborador';
  return {
    id: 'u-demo',
    email: 'demo@chronosx.app',
    displayName: audience === 'teams' ? 'Demo Manager' : 'Demo User',
    role,
    orgId: 'org-demo',
    orgName: 'ChronosX Demo Org',
    passwordMustChange: false,
  };
}

export const useAuthStore = create<AuthState>((set) => ({
  user: initialUser(),
  tokens: {
    accessToken: 'demo',
    refreshToken: 'demo',
    expiresAt: Date.now() + 3600_000,
  },
  isAuthenticated: true,
  isLoading: false,
  isRehydrating: false,
  error: null,
  login: async () => true,
  register: async () => null,
  logout: async () => {},
  refreshTokens: async () => true,
  setUser: (user) => set({ user, isAuthenticated: true }),
  setTokens: (tokens) => set({ tokens }),
  setLoading: (isLoading) => set({ isLoading }),
  setError: (error) => set({ error }),
  clearAuth: () => set({ user: null, tokens: null, isAuthenticated: false }),
}));

export const selectUser = (state: AuthState) => state.user;
export const selectIsAuthenticated = (state: AuthState) => state.isAuthenticated;
export const selectIsLoading = (state: AuthState) => state.isLoading;
export const selectError = (state: AuthState) => state.error;
export const selectAccessToken = (state: AuthState) => state.tokens?.accessToken;

