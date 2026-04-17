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
  error: string | null;
  login: (email: string, password: string) => Promise<boolean>;
  logout: () => Promise<void>;
  refreshTokens: () => Promise<boolean>;
  setUser: (user: User) => void;
  setTokens: (tokens: AuthTokens) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;
  clearAuth: () => void;
};

export const useAuthStore = create<AuthState>((set) => ({
  user: {
    id: 'u-demo',
    email: 'demo@chronosx.app',
    displayName: 'Demo Manager',
    role: 'Gestor',
    orgId: 'org-demo',
    orgName: 'ChronosX Demo Org',
    passwordMustChange: false,
  },
  tokens: {
    accessToken: 'demo',
    refreshToken: 'demo',
    expiresAt: Date.now() + 3600_000,
  },
  isAuthenticated: true,
  isLoading: false,
  error: null,
  login: async () => true,
  logout: async () => {},
  refreshTokens: async () => true,
  setUser: (user) => set({ user, isAuthenticated: true }),
  setTokens: (tokens) => set({ tokens }),
  setLoading: (isLoading) => set({ isLoading }),
  setError: (error) => set({ error }),
  clearAuth: () => set({ user: null, tokens: null, isAuthenticated: false }),
}));

