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
}

export interface DailySummaryDayItem {
  date: string;
  totalActiveSeconds: number;
  totalIdleSeconds: number;
  /** Razão de produtividade (0.0 a 1.0) */
  productivityRatio: number;
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
// HELPER TYPES
// ============================================================================

export type ProductivityCategory = 'productive' | 'neutral' | 'distraction';

export type GroupByOption = 'day' | 'week' | 'month';

export type PeriodPreset = 'today' | 'this_week' | 'this_month' | 'last_30_days' | 'last_90_days' | 'custom';

export interface DateRange {
  startDate: string;
  endDate: string;
}

// Period preset helpers
export const PERIOD_PRESETS: Record<PeriodPreset, () => DateRange> = {
  today: () => {
    const today = new Date().toISOString().split('T')[0];
    return { startDate: today, endDate: today };
  },
  this_week: () => {
    const now = new Date();
    const dayOfWeek = now.getDay();
    const monday = new Date(now);
    monday.setDate(now.getDate() - (dayOfWeek === 0 ? 6 : dayOfWeek - 1));
    return {
      startDate: monday.toISOString().split('T')[0],
      endDate: now.toISOString().split('T')[0],
    };
  },
  this_month: () => {
    const now = new Date();
    const firstDay = new Date(now.getFullYear(), now.getMonth(), 1);
    return {
      startDate: firstDay.toISOString().split('T')[0],
      endDate: now.toISOString().split('T')[0],
    };
  },
  last_30_days: () => {
    const now = new Date();
    const start = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);
    return {
      startDate: start.toISOString().split('T')[0],
      endDate: now.toISOString().split('T')[0],
    };
  },
  last_90_days: () => {
    const now = new Date();
    const start = new Date(now.getTime() - 90 * 24 * 60 * 60 * 1000);
    return {
      startDate: start.toISOString().split('T')[0],
      endDate: now.toISOString().split('T')[0],
    };
  },
  custom: () => ({ startDate: '', endDate: '' }),
};
