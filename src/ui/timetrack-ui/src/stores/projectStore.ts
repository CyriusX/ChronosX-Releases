/**
 * Project Store - Zustand state management for projects
 */

import { create } from 'zustand';
import { useAuthStore } from './authStore';
import { getApiBaseUrl } from '../services/apiBase';

// ============================================================================
// TYPES
// ============================================================================

export interface Project {
  id: string;
  createdByUserId: string | null;
  name: string;
  description?: string;
  color: string;
  status: string;
  createdAt: string;
  updatedAt?: string;
  isBillable: boolean;
  currency: string | null;
  hourlyRate: number | null;
  canArchive: boolean;
  canDelete: boolean;
  canReactivate: boolean;
}

interface ListProjectsResponse {
  projects: Project[];
  totalCount: number;
}

interface ProjectState {
  projects: Project[];
  isLoading: boolean;
  error: string | null;
  fetchProjects: (activeOnly?: boolean, mineOnly?: boolean) => Promise<void>;
  createProject: (name: string, description?: string, color?: string, isBillable?: boolean, currency?: string | null, hourlyRate?: number | null) => Promise<Project | null>;
  updateProject: (id: string, name: string, description?: string, color?: string, isBillable?: boolean, currency?: string | null, hourlyRate?: number | null) => Promise<Project | null>;
  archiveProject: (id: string) => Promise<boolean>;
  reactivateProject: (id: string) => Promise<boolean>;
  deleteProject: (id: string) => Promise<boolean>;
  setError: (error: string | null) => void;
}

// ============================================================================
// API HELPERS
// ============================================================================

const apiBase = () => getApiBaseUrl();

async function getAuthHeaders(): Promise<HeadersInit> {
  const tokens = useAuthStore.getState().tokens;
  return {
    'Content-Type': 'application/json',
    ...(tokens?.accessToken && { Authorization: `Bearer ${tokens.accessToken}` }),
  };
}

async function fetchProjectsApi(activeOnly?: boolean, mineOnly?: boolean): Promise<ListProjectsResponse> {
  const headers = await getAuthHeaders();
  const params = new URLSearchParams();
  if (activeOnly) params.append('activeOnly', 'true');
  if (mineOnly) params.append('mineOnly', 'true');

  const url = `${apiBase()}/projects${params.toString() ? `?${params.toString()}` : ''}`;
  const response = await fetch(url, { headers });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to fetch projects' }));
    throw new Error(error.message || error.title || 'Failed to fetch projects');
  }

  return response.json();
}

async function createProjectApi(name: string, description?: string, color?: string, isBillable?: boolean, currency?: string | null, hourlyRate?: number | null): Promise<Project> {
  const headers = await getAuthHeaders();
  const response = await fetch(`${apiBase()}/projects`, {
    method: 'POST',
    headers,
    body: JSON.stringify({ name, description, color, isBillable, currency, hourlyRate }),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to create project' }));
    throw new Error(error.message || error.title || 'Failed to create project');
  }

  return response.json();
}

async function updateProjectApi(id: string, name: string, description?: string, color?: string, isBillable?: boolean, currency?: string | null, hourlyRate?: number | null): Promise<Project> {
  const headers = await getAuthHeaders();
  const response = await fetch(`${apiBase()}/projects/${id}`, {
    method: 'PUT',
    headers,
    body: JSON.stringify({ name, description, color, isBillable, currency, hourlyRate }),
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to update project' }));
    throw new Error(error.message || error.title || 'Failed to update project');
  }

  return response.json();
}

async function archiveProjectApi(id: string): Promise<void> {
  const headers = await getAuthHeaders();
  const response = await fetch(`${apiBase()}/projects/${id}/archive`, {
    method: 'POST',
    headers,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to archive project' }));
    throw new Error(error.message || error.title || 'Failed to archive project');
  }
}

async function reactivateProjectApi(id: string): Promise<void> {
  const headers = await getAuthHeaders();
  const response = await fetch(`${apiBase()}/projects/${id}/reactivate`, {
    method: 'POST',
    headers,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to reactivate project' }));
    throw new Error(error.message || error.title || 'Failed to reactivate project');
  }
}

async function deleteProjectApi(id: string): Promise<void> {
  const headers = await getAuthHeaders();
  const response = await fetch(`${apiBase()}/projects/${id}`, {
    method: 'DELETE',
    headers,
  });

  if (!response.ok) {
    const error = await response.json().catch(() => ({ message: 'Failed to delete project' }));
    throw new Error(error.message || error.title || 'Failed to delete project');
  }
}

// ============================================================================
// STORE
// ============================================================================

export const useProjectStore = create<ProjectState>((set, get) => ({
  projects: [],
  isLoading: false,
  error: null,

  fetchProjects: async (activeOnly?: boolean, mineOnly?: boolean) => {
    set({ isLoading: true, error: null });

    try {
      const result = await fetchProjectsApi(activeOnly, mineOnly);
      set({ projects: result.projects, isLoading: false, error: null });
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to fetch projects';
      set({ isLoading: false, error: message });
    }
  },

  createProject: async (name: string, description?: string, color?: string, isBillable?: boolean, currency?: string | null, hourlyRate?: number | null) => {
    set({ isLoading: true, error: null });

    try {
      const project = await createProjectApi(name, description, color, isBillable, currency, hourlyRate);
      const { projects } = get();
      set({ projects: [...projects, project], isLoading: false, error: null });
      return project;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to create project';
      set({ isLoading: false, error: message });
      return null;
    }
  },

  updateProject: async (id: string, name: string, description?: string, color?: string, isBillable?: boolean, currency?: string | null, hourlyRate?: number | null) => {
    set({ isLoading: true, error: null });

    try {
      const updatedProject = await updateProjectApi(id, name, description, color, isBillable, currency, hourlyRate);
      const { projects } = get();
      set({
        projects: projects.map(p => p.id === id ? updatedProject : p),
        isLoading: false,
        error: null,
      });
      return updatedProject;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to update project';
      set({ isLoading: false, error: message });
      return null;
    }
  },

  archiveProject: async (id: string) => {
    set({ isLoading: true, error: null });

    try {
      await archiveProjectApi(id);
      const { projects } = get();
      set({
        projects: projects.map(p =>
          p.id === id ? { ...p, status: 'Archived', canArchive: false, canReactivate: p.canDelete } : p
        ),
        isLoading: false,
        error: null,
      });
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to archive project';
      set({ isLoading: false, error: message });
      return false;
    }
  },

  reactivateProject: async (id: string) => {
    set({ isLoading: true, error: null });

    try {
      await reactivateProjectApi(id);
      const { projects } = get();
      set({
        projects: projects.map(p =>
          p.id === id ? { ...p, status: 'Active', canArchive: p.canDelete, canReactivate: false } : p
        ),
        isLoading: false,
        error: null,
      });
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to reactivate project';
      set({ isLoading: false, error: message });
      return false;
    }
  },

  deleteProject: async (id: string) => {
    set({ isLoading: true, error: null });

    try {
      await deleteProjectApi(id);
      const { projects } = get();
      set({
        projects: projects.filter(p => p.id !== id),
        isLoading: false,
        error: null,
      });
      return true;
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Failed to delete project';
      set({ isLoading: false, error: message });
      return false;
    }
  },

  setError: (error) => set({ error }),
}));

// ============================================================================
// SELECTORS
// ============================================================================

export const selectProjects = (state: ProjectState) => state.projects;
export const selectActiveProjects = (state: ProjectState) =>
  state.projects.filter(p => p.status === 'Active');
export const selectArchivedProjects = (state: ProjectState) =>
  state.projects.filter(p => p.status === 'Archived');
export const selectIsLoading = (state: ProjectState) => state.isLoading;
export const selectError = (state: ProjectState) => state.error;
