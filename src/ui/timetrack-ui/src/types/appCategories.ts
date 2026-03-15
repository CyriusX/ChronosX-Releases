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
 */
export type AppSubcategory =
  | 'development'
  | 'communication'
  | 'productivity'
  | 'design'
  | 'social_media'
  | 'entertainment'
  | 'news'
  | 'shopping'
  | 'finance'
  | 'utilities'
  | 'unknown';

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
  development: { label: 'Development', labelPt: 'Desenvolvimento' },
  communication: { label: 'Communication', labelPt: 'Comunicação' },
  productivity: { label: 'Productivity', labelPt: 'Produtividade' },
  design: { label: 'Design', labelPt: 'Design' },
  social_media: { label: 'Social Media', labelPt: 'Redes Sociais' },
  entertainment: { label: 'Entertainment', labelPt: 'Entretenimento' },
  news: { label: 'News', labelPt: 'Notícias' },
  shopping: { label: 'Shopping', labelPt: 'Compras' },
  finance: { label: 'Finance', labelPt: 'Finanças' },
  utilities: { label: 'Utilities', labelPt: 'Utilitários' },
  unknown: { label: 'Unknown', labelPt: 'Desconhecido' },
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
  productive: ['development', 'communication', 'productivity', 'design', 'finance'],
  neutral: ['utilities', 'news', 'shopping'],
  distraction: ['social_media', 'entertainment'],
};
