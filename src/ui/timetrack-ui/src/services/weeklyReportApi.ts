import { api } from './apiClient';

export interface ReportPreferences {
  includeTeamComparison: boolean;
  includeDifficultyAnalysis: boolean;
  includeWeekOverWeek: boolean;
  includeUnproductiveDays: boolean;
}

export interface WeeklyReportSchedule {
  id: string;
  dayOfWeek: number;
  dayName: string;
  timeOfDay: string;
  isEnabled: boolean;
  preferences: ReportPreferences;
  createdAt: string;
  updatedAt: string | null;
}

export interface WeeklyReportScheduleRequest {
  dayOfWeek: number;
  timeOfDay: string;
  isEnabled: boolean;
  preferences?: ReportPreferences;
}

export interface WeeklyReportPreview {
  htmlReport: string;
  period: string;
}

export async function getWeeklyReportSchedule(): Promise<WeeklyReportSchedule | null> {
  return api.get<WeeklyReportSchedule | null>('/weekly-report-schedule');
}

export async function upsertWeeklyReportSchedule(
  request: WeeklyReportScheduleRequest
): Promise<WeeklyReportSchedule> {
  return api.put<WeeklyReportSchedule>('/weekly-report-schedule', request);
}

export async function deleteWeeklyReportSchedule(): Promise<void> {
  await api.delete('/weekly-report-schedule');
}

export async function previewWeeklyReport(): Promise<WeeklyReportPreview> {
  return api.post<WeeklyReportPreview>('/weekly-report-schedule/preview');
}
