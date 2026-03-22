/**
 * Hooks barrel export
 */

export { useIpc, useIpcConnection } from './useIpc';
export { useDashboardData } from './useDashboardData';
export { useMembers } from './useMembers';
export { usePermissions } from './usePermissions';
export {
  useFocusMode,
  formatRemaining,
  getFocusStateLabel,
  getFocusModeEmoji,
  getBreakTypeLabel,
} from './useFocusMode';
export type { UseFocusModeReturn } from './useFocusMode';
export { useFocusModePolicy } from './useFocusModePolicy';
export type { UseFocusModePolicyReturn } from './useFocusModePolicy';

// Reports hooks
export { useReportsData, useReportsSummary } from './useReportsData';
export type {
  ReportsDataState,
  ReportsFilters,
  UseReportsDataOptions,
  UseReportsDataReturn,
} from './useReportsData';
