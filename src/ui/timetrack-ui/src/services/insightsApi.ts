import { api } from './apiClient';

export interface WeeklyNarrativeResponse {
  narrative: string | null;
  weekStart: string;
  modelVersion?: string;
  wasReviewed?: boolean;
  reviewOutcome?: string | null;
}

export interface PatternItem {
  id: string;
  patternTag: string;
  strength: number;
  description: string | null;
  detectedAt: string;
}

export interface WeeklyTrendItem {
  weekStart: string;
  avgFocusScore: number;
  avgProductiveRatio: number;
  totalActiveHours: number;
  trendFocusScore: number | null;
}

export interface BenchmarkResponse {
  hasData: boolean;
  user?: {
    avgFocusScore: number;
    avgProductiveRatio: number;
  };
  team?: {
    avgFocusScore: number;
    avgProductiveRatio: number;
    userCount: number;
  };
  focusDiff?: number;
  focusDiffPercent?: number;
}

export interface NarrativeHistoryItem {
  id: string;
  narrative: string | null;
  createdAt: string;
  wasReviewed: boolean;
  reviewOutcome: string | null;
}

export async function getWeeklyNarrative(weekStart?: string): Promise<WeeklyNarrativeResponse> {
  try {
    const params = weekStart ? `?weekStart=${weekStart}` : '';
    return await api.get<WeeklyNarrativeResponse>(`/reports/weekly-narrative${params}`);
  } catch {
    return { narrative: null, weekStart: weekStart ?? '' };
  }
}

export async function getPatterns(): Promise<PatternItem[]> {
  try {
    return await api.get<PatternItem[]>('/reports/patterns');
  } catch {
    return [];
  }
}

export async function getFocusTrend(): Promise<WeeklyTrendItem[]> {
  try {
    return await api.get<WeeklyTrendItem[]>('/reports/focus-trend');
  } catch {
    return [];
  }
}

export async function getBenchmark(): Promise<BenchmarkResponse> {
  try {
    return await api.get<BenchmarkResponse>('/reports/benchmark');
  } catch {
    return { hasData: false };
  }
}

export async function getNarrativesHistory(limit: number = 12): Promise<NarrativeHistoryItem[]> {
  try {
    return await api.get<NarrativeHistoryItem[]>(`/reports/narratives/history?limit=${limit}`);
  } catch {
    return [];
  }
}

export async function submitNarrativeFeedback(
  decisionId: string,
  outcome: 'useful' | 'not_useful'
): Promise<void> {
  await api.post('/ai/feedback', { decision_id: decisionId, outcome });
}

export interface InsightItem {
  type: 'distraction_app' | 'focus_score' | 'deep_focus' | 'context_switches' | 'milestone' | 'suggestion';
  severity: 'positive' | 'neutral' | 'negative';
  message: string;
}

export interface LiveInsightResponse {
  hasData: boolean;
  focusEstimate: number;
  productiveSeconds: number;
  distractionSeconds: number;
  neutralSeconds: number;
  contextSwitches: number;
  longestFocusMinutes: number;
  trend: 'improving' | 'declining' | 'stable';
  insights: InsightItem[];
  latestAlert: {
    id: string;
    alertType: string;
    message: string;
    severity: string;
    createdAt: string;
  } | null;
}

export interface TeamLiveInsightMember {
  userId: string;
  userName: string;
  focusEstimate: number;
  activeSeconds: number;
  latestAlert: {
    id: string;
    alertType: string;
    message: string;
    severity: string;
  } | null;
}

export interface TeamLiveInsightsResponse {
  members: TeamLiveInsightMember[];
  orgAlert: {
    id: string;
    alertType: string;
    message: string;
    severity: string;
    createdAt: string;
  } | null;
}

export async function getLiveInsight(): Promise<LiveInsightResponse> {
  try {
    return await api.get<LiveInsightResponse>('/reports/live-insight');
  } catch {
    return { hasData: false, focusEstimate: 0, productiveSeconds: 0, distractionSeconds: 0, neutralSeconds: 0, contextSwitches: 0, longestFocusMinutes: 0, trend: 'stable', insights: [], latestAlert: null };
  }
}

export async function getTeamLiveInsights(): Promise<TeamLiveInsightsResponse> {
  try {
    return await api.get<TeamLiveInsightsResponse>('/reports/team-live-insights');
  } catch {
    return { members: [], orgAlert: null };
  }
}

export interface ReportsInsightsComparison {
  personalAvgFocus: number | null;
  focusDiff: number | null;
  trend: 'improving' | 'declining' | 'stable';
  baselineDays: number;
}

export interface ReportsInsightsPattern {
  patternTag: string;
  strength: number;
  description: string | null;
}

export interface ReportsInsightsAnomaly {
  anomalyType: string;
  severity: string;
}

export interface ReportsInsightsAlert {
  id: string;
  alertType: string;
  message: string;
  severity: string;
  actionType?: string;
}

export interface ReportsInsightsBenchmark {
  hasData: boolean;
  user: { avgFocusScore: number; avgProductiveRatio: number };
  team: { avgFocusScore: number; avgProductiveRatio: number; userCount: number };
  focusDiff: number;
}

export interface ReportsTeamSummary {
  avgFocusScore: number;
  memberCount: number;
  topPatterns: ReportsInsightsPattern[];
  totalAnomalies: number;
  totalAlerts: number;
}

export interface ReportsInsightsResponse {
  hasData: boolean;
  isTeamView: boolean;
  dateRange: { startDate: string; endDate: string };
  focusScore?: number;
  insight?: string;
  suggestion?: string;
  summary?: {
    totalTrackedHours: number;
    productiveHours: number;
    distractionHours: number;
    focusScore: number;
    contextSwitchesPerDay: number;
    trend: string;
  };
  topApps?: string[];
  topDistractions?: string[];
  comparison?: ReportsInsightsComparison;
  patterns?: ReportsInsightsPattern[];
  anomalies?: ReportsInsightsAnomaly[];
  alerts?: ReportsInsightsAlert[];
  benchmark?: ReportsInsightsBenchmark | null;
  teamSummary?: ReportsTeamSummary;
}

export async function getReportsInsights(
  startDate: string,
  endDate: string,
  userId?: string
): Promise<ReportsInsightsResponse> {
  try {
    const params = new URLSearchParams({ startDate, endDate });
    if (userId === 'all') {
      params.append('allTeam', 'true');
    } else if (userId) {
      params.append('userId', userId);
    }
    return await api.get<ReportsInsightsResponse>(`/reports/reports-insights?${params.toString()}`);
  } catch {
    return { hasData: false, isTeamView: false, dateRange: { startDate, endDate } };
  }
}
