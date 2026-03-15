/**
 * AppCategoriesList - List component for displaying apps (CX-144)
 *
 * SOLID:
 * - SRP: Apenas exibição de lista de apps
 * - OCP: Extensível para novas colunas/ações
 *
 * Composition:
 * - Renderiza lista de AppCategoryRow
 */

import { Trash2, Search, Filter } from 'lucide-react';
import type {
  AppCategoryDisplayItem,
  AppCategoryFilter,
} from '../../types/appCategories';
import { PRODUCTIVITY_CONFIG, SUBCATEGORIES } from '../../types/appCategories';

interface AppCategoriesListProps {
  apps: AppCategoryDisplayItem[];
  isLoading: boolean;
  error: string | null;
  searchQuery: string;
  activeFilter: AppCategoryFilter;
  uncategorizedCount: number;
  onSearchChange: (query: string) => void;
  onFilterChange: (filter: AppCategoryFilter) => void;
  onEdit: (app: AppCategoryDisplayItem) => void;
  onDelete: (identifier: string) => void;
  onAdd: () => void;
}

const FILTER_OPTIONS: { value: AppCategoryFilter; label: string }[] = [
  { value: 'all', label: 'Todos' },
  { value: 'productive', label: 'Produtivos' },
  { value: 'distraction', label: 'Distrações' },
  { value: 'overrides', label: 'Overrides' },
  { value: 'unknown', label: 'Desconhecidos' },
];

/**
 * List component for displaying and managing app categories
 */
export function AppCategoriesList({
  apps,
  isLoading,
  error,
  searchQuery,
  activeFilter,
  uncategorizedCount,
  onSearchChange,
  onFilterChange,
  onEdit,
  onDelete,
  onAdd,
}: AppCategoriesListProps) {
  return (
    <div className="space-y-4">
      {/* Header with search and filters */}
      <div className="flex flex-col sm:flex-row gap-3">
        {/* Search */}
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-[rgba(245,247,251,0.4)]" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => onSearchChange(e.target.value)}
            placeholder="Buscar app ou site..."
            className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg pl-10 pr-4 py-2.5 text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4ad9ff]"
          />
        </div>

        {/* Filter */}
        <div className="flex items-center gap-2">
          <Filter className="w-4 h-4 text-[rgba(245,247,251,0.4)]" />
          <select
            value={activeFilter}
            onChange={(e) => onFilterChange(e.target.value as AppCategoryFilter)}
            className="bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
          >
            {FILTER_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
                {option.value === 'unknown' && uncategorizedCount > 0 && ` (${uncategorizedCount})`}
              </option>
            ))}
          </select>

          {/* Add button */}
          <button
            onClick={onAdd}
            className="flex items-center gap-2 px-3 py-2 rounded-lg text-[13px] font-medium bg-[rgba(5,223,114,0.15)] text-[#05df72] border border-[rgba(5,223,114,0.3)] hover:bg-[rgba(5,223,114,0.25)] transition-colors"
          >
            <span className="text-[16px]">+</span>
            Adicionar
          </button>
        </div>
      </div>

      {/* Uncategorized count badge */}
      {uncategorizedCount > 0 && (
        <div className="flex items-center gap-2 bg-[rgba(255,193,7,0.1)] border border-[rgba(255,193,7,0.2)] rounded-lg px-3 py-2">
          <span className="text-[12px] text-[#FFC107]">
            {uncategorizedCount} app{uncategorizedCount !== 1 ? 's' : ''} sem classificação
          </span>
          <button
            onClick={() => onFilterChange('unknown')}
            className="text-[11px] text-[#4ad9ff] hover:underline"
          >
            Filtrar
          </button>
        </div>
      )}

      {/* Content */}
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl overflow-hidden">
        {/* Table header */}
        <div className="grid grid-cols-12 gap-2 px-4 py-3 border-b border-[rgba(255,255,255,0.06)] bg-[rgba(255,255,255,0.02)]">
          <div className="col-span-4 text-[11px] font-medium text-[rgba(245,247,251,0.5)] uppercase tracking-wider">
            App/Site
          </div>
          <div className="col-span-3 text-[11px] font-medium text-[rgba(245,247,251,0.5)] uppercase tracking-wider">
            Classificação
          </div>
          <div className="col-span-2 text-[11px] font-medium text-[rgba(245,247,251,0.5)] uppercase tracking-wider">
            Fonte
          </div>
          <div className="col-span-3 text-[11px] font-medium text-[rgba(245,247,251,0.5)] uppercase tracking-wider text-right">
            Ações
          </div>
        </div>

        {/* Loading state */}
        {isLoading && (
          <div className="flex items-center justify-center py-12">
            <div className="flex flex-col items-center gap-3">
              <div className="w-8 h-8 border-2 border-[#4ad9ff] border-t-transparent rounded-full animate-spin" />
              <span className="text-[13px] text-[rgba(245,247,251,0.5)]">Carregando...</span>
            </div>
          </div>
        )}

        {/* Error state */}
        {error && !isLoading && (
          <div className="flex items-center justify-center py-12">
            <div className="text-center">
              <p className="text-[14px] text-[#ff6b6b]">{error}</p>
              <button className="mt-2 text-[12px] text-[#4ad9ff] hover:underline">
                Tentar novamente
              </button>
            </div>
          </div>
        )}

        {/* Empty state */}
        {!isLoading && !error && apps.length === 0 && (
          <div className="flex items-center justify-center py-12">
            <div className="text-center">
              <p className="text-[14px] text-[rgba(245,247,251,0.5)]">
                Nenhum aplicativo encontrado
              </p>
              {searchQuery && (
                <button
                  onClick={() => onSearchChange('')}
                  className="mt-2 text-[12px] text-[#4ad9ff] hover:underline"
                >
                  Limpar busca
                </button>
              )}
            </div>
          </div>
        )}

        {/* Apps list */}
        {!isLoading && !error && apps.length > 0 && (
          <div className="divide-y divide-[rgba(255,255,255,0.04)]">
            {apps.map((app) => (
              <AppCategoryRow
                key={app.identifier}
                app={app}
                onEdit={() => onEdit(app)}
                onDelete={() => onDelete(app.identifier)}
              />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// ============================================================================
// ROW COMPONENT
// ============================================================================

interface AppCategoryRowProps {
  app: AppCategoryDisplayItem;
  onEdit: () => void;
  onDelete: () => void;
}

/**
 * Single row in the app categories list
 */
function AppCategoryRow({ app, onEdit, onDelete }: AppCategoryRowProps) {
  const prodConfig = PRODUCTIVITY_CONFIG[app.productivity];
  const subcategoryLabel = SUBCATEGORIES[app.subcategory]?.labelPt || app.subcategory;
  const isOverride = app.source === 'org_override';
  const isUnknown = app.productivity === 'unknown';

  return (
    <div className="grid grid-cols-12 gap-2 px-4 py-3 items-center hover:bg-[rgba(255,255,255,0.02)] transition-colors">
      {/* App/Site */}
      <div className="col-span-4 flex items-center gap-3">
        <div
          className="w-8 h-8 rounded-lg flex items-center justify-center text-[16px]"
          style={{ backgroundColor: `${prodConfig.color}20` }}
        >
          {prodConfig.emoji}
        </div>
        <div className="min-w-0">
          <p className="text-[13px] font-medium text-[#f5f7fb] truncate">
            {app.displayName}
          </p>
          <p className="text-[10px] text-[rgba(245,247,251,0.35)] truncate">
            {app.identifier}
          </p>
        </div>
      </div>

      {/* Classification */}
      <div className="col-span-3">
        <span
          className="inline-flex items-center gap-1.5 px-2 py-1 rounded-md text-[11px] font-medium"
          style={{
            backgroundColor: `${prodConfig.color}15`,
            color: prodConfig.color,
          }}
        >
          {prodConfig.emoji}
          {prodConfig.labelPt}/{subcategoryLabel}
        </span>
      </div>

      {/* Source */}
      <div className="col-span-2">
        {isOverride ? (
          <span className="inline-flex items-center gap-1 px-2 py-1 rounded-md text-[11px] font-medium bg-[rgba(74,217,255,0.15)] text-[#4ad9ff] border border-[rgba(74,217,255,0.3)]">
            ✏️ Override
          </span>
        ) : (
          <span className="text-[11px] text-[rgba(245,247,251,0.4)]">
            Global
          </span>
        )}
      </div>

      {/* Actions */}
      <div className="col-span-3 flex items-center justify-end gap-2">
        <button
          onClick={onEdit}
          className={`px-3 py-1.5 rounded-lg text-[12px] font-medium transition-colors ${
            isUnknown
              ? 'bg-[rgba(255,193,7,0.15)] text-[#FFC107] border border-[rgba(255,193,7,0.3)] hover:bg-[rgba(255,193,7,0.25)]'
              : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.6)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
          }`}
        >
          {isUnknown ? 'Classificar' : 'Editar'}
        </button>
        {isOverride && (
          <button
            onClick={onDelete}
            className="w-8 h-8 rounded-lg bg-[rgba(255,107,107,0.1)] border border-[rgba(255,107,107,0.2)] flex items-center justify-center hover:bg-[rgba(255,107,107,0.2)] transition-colors"
            title="Remover override"
          >
            <Trash2 className="w-3.5 h-3.5 text-[#ff6b6b]" />
          </button>
        )}
      </div>
    </div>
  );
}
