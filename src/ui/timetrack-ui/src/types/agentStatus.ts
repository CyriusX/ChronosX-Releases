/**
 * Agent Status Types for UI
 *
 * Types for displaying agent health and sync status in the UI
 */

import type { ErrorItem as IpcErrorItem } from './ipc';

// Re-export ErrorItem for convenience
export type ErrorItem = IpcErrorItem;

// Health level based on sync status and errors
export type AgentHealthLevel = 'healthy' | 'degraded' | 'unhealthy';

// UI State for AgentStatusSection
export interface AgentStatusUI {
  // Health computed from multiple sources
  health: AgentHealthLevel;

  // Status info from getCurrentStatus query
  state: 'running' | 'paused' | 'stopped' | 'idle';
  uptime: number; // seconds
  version: string;

  // Sync info from getSyncState query
  syncStatus: 'synced' | 'pending' | 'syncing' | 'failed';
  lastSyncAt?: string;
  pendingItems: number;
  failedItems: number;

  // Errors from getErrors query
  recentErrors: ErrorItem[];

  // Computed UI values
  healthIndicator: {
    color: string; // tailwind color class
    bgGradient: string; // gradient classes
    label: string;
    icon: string;
  };
  lastSyncRelative?: string; // "há 3 minutos"
}

// Helper to compute health level
export function computeAgentHealth(
  syncStatus: AgentStatusUI['syncStatus'],
  pendingItems: number,
  failedItems: number,
  recentErrors: ErrorItem[]
): AgentHealthLevel {
  // Has recent unresolved errors = unhealthy
  const hasUnresolvedErrors = recentErrors.some(e => !e.resolved);
  if (hasUnresolvedErrors) {
    return 'unhealthy';
  }

  // Failed items or sync failed = unhealthy
  if (failedItems > 0 || syncStatus === 'failed') {
    return 'unhealthy';
  }

  // Pending items or pending sync = degraded
  if (pendingItems > 0 || syncStatus === 'pending') {
    return 'degraded';
  }

  // Syncing is healthy (in progress)
  if (syncStatus === 'syncing') {
    return 'healthy';
  }

  // Synced with no issues = healthy
  return 'healthy';
}

// Helper to format relative time
export function formatRelativeTime(isoString?: string): string | undefined {
  if (!isoString) return undefined;

  const now = new Date();
  const date = new Date(isoString);
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);

  if (diffHours > 0) {
    return `há ${diffHours}h`;
  }
  if (diffMins > 0) {
    return `há ${diffMins}min`;
  }
  if (diffMs < 60000) {
    return 'agora mesmo';
  }
  return 'há poucos segundos';
}

// Helper to format uptime
export function formatUptime(seconds: number): string {
  if (seconds < 60) {
    return `${seconds}s`;
  }
  const mins = Math.floor(seconds / 60);
  if (mins < 60) {
    return `${mins}min`;
  }
  const hours = Math.floor(mins / 60);
  const remainingMins = mins % 60;
  if (hours < 24) {
    return `${hours}h ${remainingMins}min`;
  }
  const days = Math.floor(hours / 24);
  const remainingHours = hours % 24;
  return `${days}d ${remainingHours}h`;
}

// Health indicator configuration for UI
export const HEALTH_CONFIG: Record<AgentHealthLevel, AgentStatusUI['healthIndicator']> = {
  healthy: {
    color: 'text-[#4ade96]',
    bgGradient: 'from-[rgba(74,222,128,0.3)] to-[rgba(74,222,128,0.1)]',
    label: 'Saudável',
    icon: 'text-[#4ade96]',
  },
  degraded: {
    color: 'text-[#fbbf24]',
    bgGradient: 'from-[rgba(251,191,36,0.3)] to-[rgba(251,191,36,0.1)]',
    label: 'Atrasado',
    icon: 'text-[#fbbf24]',
  },
  unhealthy: {
    color: 'text-[#f87171]',
    bgGradient: 'from-[rgba(248,113,113,0.3)] to-[rgba(248,113,113,0.1)]',
    label: 'Offline',
    icon: 'text-[#f87171]',
  },
};
