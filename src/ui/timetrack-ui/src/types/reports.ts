/**
 * Report Types - Types for report API responses
 *
 * SRP: Apenas define tipos para responses de API
 * OCP: Extensível para novos tipos de relatório
 */

// ============================================================================
// DAILY SUMMARY - Resumo de um único dia
// ============================================================================

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
  appCategory?: string;
}

// ============================================================================
// TOP APPS - Apps mais usados
// ============================================================================

export interface TopAppsResponse {
  apps: TopAppItem[];
}

export interface TopAppItem {
  displayName: string;
  totalSeconds: number;
  sessionCount: number;
  /** Categoria de produtividade: productive | neutral | distraction */
  productivity?: string;
  /** Subcategoria: development, social_media, etc. */
  subcategory?: string;
  /** Percentual do tempo total */
  percentage?: number;
}

// ============================================================================
// DAILY SUMMARY RANGE - Heatmap estilo GitHub (CX-155)
// ============================================================================

export interface DailySummaryRangeResponse {
  days: DailySummaryDayItem[];
  /** Focus Score agregado do período (0-100) */
  periodFocusScore: number;
  /** Proporção base de produtividade do período (0.0 a 1.0) */
  periodBaseProductivity: number;
}

export interface DailySummaryDayItem {
  date: string;
  totalActiveSeconds: number;
  totalIdleSeconds: number;
  /** Razão de produtividade (0.0 a 1.0) */
  productivityRatio: number;
  /** Focus Score do dia (0-100) */
  focusScore: number;
}

// ============================================================================
// PRODUCTIVITY TREND - Gráfico de barras empilhadas (CX-155)
// ============================================================================

export interface ProductivityTrendResponse {
  periods: ProductivityTrendPeriodItem[];
}

export interface ProductivityTrendPeriodItem {
  period: string;
  productiveSeconds: number;
  neutralSeconds: number;
  distractionSeconds: number;
  idleSeconds: number;
}

// ============================================================================
// TOP PATHS - URLs e caminhos mais acessados (CX-155)
// ============================================================================

export interface TopPathsResponse {
  paths: TopPathItem[];
}

export interface TopPathItem {
  /** Título principal (nome do projeto, site, ou aplicação) */
  title: string;
  /** Caminho do arquivo ou URL extraído */
  filePath?: string;
  /** URL ou caminho completo (legado, para compatibilidade) */
  path: string;
  sourceApp: string;
  totalSeconds: number;
  visitCount: number;
}

// ============================================================================
// TOP FOLDERS - Pastas mais acessadas (CX-xxx)
// ============================================================================

export interface TopFoldersResponse {
  folders: TopFolderItem[];
}

export interface TopFolderItem {
  folderPath: string;
  totalSeconds: number;
  visitCount: number;
}

// ============================================================================
// DISTRACTION STATS - Estatísticas de distração (CX-155)
// ============================================================================

export interface DistractionStatsResponse {
  dailyDistractions: DailyDistractionItem[];
  topDistractions: TopDistractionItem[];
}

export interface DailyDistractionItem {
  date: string;
  distractionSeconds: number;
}

export interface TopDistractionItem {
  displayName: string;
  processName: string;
  totalSeconds: number;
  sessionCount: number;
  subcategory?: string;
}

// ============================================================================
// CATEGORY DISTRIBUTION - Distribuição por categoria (CX-155)
// ============================================================================

export interface CategoryDistributionResponse {
  categories: CategoryDistributionItem[];
}

export interface CategoryDistributionItem {
  category: string;
  totalSeconds: number;
  percentage: number;
  subcategories: SubcategoryItem[];
}

export interface SubcategoryItem {
  name: string;
  totalSeconds: number;
  percentage: number;
}

// ============================================================================
// REPORTS BUNDLE - Single-call composite response
// ============================================================================

export interface ReportsBundleResponse {
  dailySummaryRange: DailySummaryRangeResponse;
  productivityTrend: ProductivityTrendResponse;
  topApps: TopAppsResponse;
  topPaths: TopPathsResponse;
  topFolders?: TopFoldersResponse;
  distractionStats: DistractionStatsResponse;
  categoryDistribution: CategoryDistributionResponse;
}

// ============================================================================
// HELPER TYPES
// ============================================================================

export type ProductivityCategory = 'productive' | 'neutral' | 'distraction';

export type GroupByOption = 'day' | 'week' | 'month';

export type PeriodPreset = 'today' | 'this_week' | 'this_month' | 'last_30_days' | 'last_90_days' | 'custom';

export interface DateRange {
  startDate: string;
  endDate: string;
}

/**
 * Format a Date as a local date string (YYYY-MM-DD) using the user's timezone.
 * Unlike toISOString().split('T')[0], this doesn't convert to UTC first,
 * so a user at 22:00 local time gets today's local date, not tomorrow's UTC date.
 */
export function toLocalDateStr(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Returns the user's IANA timezone (e.g. "America/Sao_Paulo") */
export function getUserTimezone(): string {
  try {
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
    // Validate it looks like an IANA timezone (contains '/')
    // Abbreviations like "EDT", "PST" are NOT valid IANA identifiers
    // and cause the backend to fall back to UTC with wrong day boundaries.
    if (tz && tz.includes('/')) {
      return tz;
    }
    // Fallback: try to resolve from the UTC offset
    const offset = new Date().getTimezoneOffset(); // minutes, negative = ahead of UTC
    if (offset === 180) return 'America/Sao_Paulo';
    if (offset === 240) return 'America/New_York';
    if (offset === 300) return 'America/Chicago';
    if (offset === 360) return 'America/Denver';
    if (offset === 420) return 'America/Los_Angeles';
    if (offset === 0) return 'Europe/London';
    if (offset === -60) return 'Europe/Berlin';
    if (offset === -120) return 'Europe/Helsinki';
    // Generic UTC offset as last resort
    const h = Math.abs(Math.floor(offset / 60));
    const sign = offset <= 0 ? '+' : '-';
    return `Etc/GMT${sign}${h}`;
  } catch {
    return 'UTC';
  }
}

// Period preset helpers
export const PERIOD_PRESETS: Record<PeriodPreset, () => DateRange> = {
  today: () => {
    const today = toLocalDateStr(new Date());
    return { startDate: today, endDate: today };
  },
  this_week: () => {
    const now = new Date();
    const dayOfWeek = now.getDay();
    const monday = new Date(now);
    monday.setDate(now.getDate() - (dayOfWeek === 0 ? 6 : dayOfWeek - 1));
    return {
      startDate: toLocalDateStr(monday),
      endDate: toLocalDateStr(now),
    };
  },
  this_month: () => {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
    return {
      startDate: toLocalDateStr(firstDay),
      endDate: toLocalDateStr(now),
    };
  },
  last_30_days: () => {
    const now = new Date();
    const start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
    return {
      startDate: toLocalDateStr(start),
      endDate: toLocalDateStr(now),
    };
  },
  last_90_days: () => {
    const now = new Date();
    const start = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
    return {
      startDate: toLocalDateStr(start),
      endDate: toLocalDateStr(now),
    };
  },
  custom: () => ({ startDate: '', endDate: '' }),
};
