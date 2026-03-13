/**
 * Report Types - Types for report API responses
 */

export interface DailySummaryResponse {
  date: string;
  totalActiveSeconds: number;
  totalIdleSeconds: number;
  firstActivity: string | null;
  lastActivity: string | null;
  apps: DailyAppSummary[];
}

export interface DailyAppSummary {
  displayName: string;
  totalSeconds: number;
  sessionCount: number;
}

export interface TopAppsResponse {
  apps: TopAppItem[];
}

export interface TopAppItem {
  displayName: string;
  totalSeconds: number;
  sessionCount: number;
}
