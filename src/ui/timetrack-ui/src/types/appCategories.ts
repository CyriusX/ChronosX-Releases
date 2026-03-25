/**
 * Types for App Categories (CX-143/CX-144)
 *
 * SOLID:
 * - ISP: Interfaces segregadas por responsabilidade
 * - DIP: Contratos para comunicação com backend
 */

// ============================================================================
// PRODUCTIVITY & SUBCATEGORIES
// ============================================================================

/**
 * Productivity classification
 */
export type ProductivityCategory = 'productive' | 'neutral' | 'distraction' | 'unknown';

/**
 * Subcategory types for granular classification
 * Must match backend AppSubcategory enum (snake_case → PascalCase via ToPascalCase)
 */
export type AppSubcategory =
  // Productive
  | 'development'
  | 'design'
  | 'communication'
  | 'productivity_tools'
  | 'meetings'
  | 'documentation'
  | 'dev_ops'
  | 'finance'
  // Neutral
  | 'browser_general'
  | 'system'
  | 'unknown'
  | 'file_manager'
  | 'utilities'
  // Distraction
  | 'social_media'
  | 'entertainment'
  | 'gaming'
  | 'news'
  | 'music_streaming'
  | 'shopping';

/**
 * Identifier type
 */
export type IdentifierType = 'exe' | 'domain';

/**
 * Source of classification
 */
export type CategorySource = 'global' | 'org_override' | 'default';

// ============================================================================
// API RESPONSE TYPES
// ============================================================================

/**
 * App category response (merged global + override)
 */
export interface AppCategoryResponse {
  identifier: string;
  identifierType: IdentifierType;
  displayName: string;
  productivity: ProductivityCategory;
  subcategory: AppSubcategory;
  source: CategorySource;
  note?: string;
}

/**
 * Override response with full details
 */
export interface AppCategoryOverrideResponse extends AppCategoryResponse {
  id: string;
  orgId: string;
  createdBy: string;
  createdByName: string;
  createdAt: string;
  updatedAt: string;
}

/**
 * Request for creating/updating an override
 */
export interface UpsertAppCategoryOverrideRequest {
  identifier: string;
  identifierType: IdentifierType;
  displayName?: string;
  productivity: ProductivityCategory;
  subcategory: AppSubcategory;
  note?: string;
}

/**
 * Paginated list of overrides
 */
export interface AppCategoryOverrideListResponse {
  overrides: AppCategoryOverrideResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

/**
 * Global category search item
 */
export interface GlobalCategoryItem {
  identifier: string;
  identifierType: IdentifierType;
  displayName: string;
  productivity: ProductivityCategory;
  subcategory: AppSubcategory;
  hasOverride: boolean;
}

/**
 * Global category search response
 */
export interface GlobalCategorySearchResponse {
  items: GlobalCategoryItem[];
  totalCount: number;
}

// ============================================================================
// USAGE STATISTICS
// ============================================================================

/**
 * Category usage item for statistics
 */
export interface CategoryUsageItem {
  identifier: string;
  displayName: string;
  productivity: ProductivityCategory;
  subcategory: AppSubcategory;
  totalMinutes: number;
  sessionCount: number;
  source: CategorySource;
}

/**
 * Uncategorized app needing classification
 */
export interface UncategorizedAppItem {
  identifier: string;
  identifierType: IdentifierType;
  totalMinutes: number;
  sessionCount: number;
  userCount: number;
}

/**
 * Usage statistics response
 */
export interface CategoryUsageStatsResponse {
  topProductiveApps: CategoryUsageItem[];
  topNeutralApps: CategoryUsageItem[];
  topDistractionApps: CategoryUsageItem[];
  uncategorizedApps: UncategorizedAppItem[];
  totalAppsUsed: number;
  categorizedApps: number;
  uncategorizedCount: number;
}

// ============================================================================
// UI STATE TYPES
// ============================================================================

/**
 * Filter options for app list
 */
export type AppCategoryFilter = 'all' | 'productive' | 'neutral' | 'distraction' | 'overrides' | 'unknown';

/**
 * Combined app item for UI display
 */
export interface AppCategoryDisplayItem {
  identifier: string;
  identifierType: IdentifierType;
  displayName: string;
  productivity: ProductivityCategory;
  subcategory: AppSubcategory;
  source: CategorySource;
  note?: string;
  totalMinutes?: number;
  sessionCount?: number;
  userCount?: number;
  hasOverride?: boolean;
}

// ============================================================================
// SUBCATEGORIES CONFIG
// ============================================================================

/**
 * Subcategory display configuration
 */
export const SUBCATEGORIES: Record<AppSubcategory, { label: string; labelPt: string }> = {
  // Productive
  development: { label: 'Development', labelPt: 'Desenvolvimento' },
  design: { label: 'Design', labelPt: 'Design' },
  communication: { label: 'Communication', labelPt: 'Comunicação' },
  productivity_tools: { label: 'Productivity Tools', labelPt: 'Ferramentas de Produtividade' },
  meetings: { label: 'Meetings', labelPt: 'Reuniões' },
  documentation: { label: 'Documentation', labelPt: 'Documentação' },
  dev_ops: { label: 'DevOps', labelPt: 'DevOps' },
  finance: { label: 'Finance', labelPt: 'Finanças' },
  // Neutral
  browser_general: { label: 'Browser', labelPt: 'Navegador' },
  system: { label: 'System', labelPt: 'Sistema' },
  unknown: { label: 'Unknown', labelPt: 'Desconhecido' },
  file_manager: { label: 'File Manager', labelPt: 'Gerenciador de Arquivos' },
  utilities: { label: 'Utilities', labelPt: 'Utilitários' },
  // Distraction
  social_media: { label: 'Social Media', labelPt: 'Redes Sociais' },
  entertainment: { label: 'Entertainment', labelPt: 'Entretenimento' },
  gaming: { label: 'Gaming', labelPt: 'Jogos' },
  news: { label: 'News', labelPt: 'Notícias' },
  music_streaming: { label: 'Music/Streaming', labelPt: 'Música/Streaming' },
  shopping: { label: 'Shopping', labelPt: 'Compras' },
};

/**
 * Productivity display configuration
 */
export const PRODUCTIVITY_CONFIG: Record<ProductivityCategory, { label: string; labelPt: string; color: string; emoji: string }> = {
  productive: { label: 'Productive', labelPt: 'Produtivo', color: '#4CAF50', emoji: '🟢' },
  neutral: { label: 'Neutral', labelPt: 'Neutro', color: '#FFC107', emoji: '🟡' },
  distraction: { label: 'Distraction', labelPt: 'Distração', color: '#F44336', emoji: '🔴' },
  unknown: { label: 'Unknown', labelPt: 'Desconhecido', color: '#9E9E9E', emoji: '❓' },
};

/**
 * Subcategories grouped by productivity
 */
export const SUBCATEGORIES_BY_PRODUCTIVITY: Record<string, AppSubcategory[]> = {
  productive: ['development', 'design', 'communication', 'productivity_tools', 'meetings', 'documentation', 'dev_ops', 'finance'],
  neutral: ['browser_general', 'system', 'file_manager', 'utilities', 'unknown'],
  distraction: ['social_media', 'entertainment', 'gaming', 'news', 'music_streaming', 'shopping'],
};
