/**
 * AppCategoriesSection - Main section for app categorization management (CX-144)
 *
 * SOLID:
 * - SRP: Apenas orquestração da UI de categorização
 * - DIP: Delega lógica para hook e serviços
 * - OCP: Extensível para novas funcionalidades
 *
 * Composition:
 * - Compõe AppCategoriesList + modais
 * - Usa useAppCategories hook para estado
 */

import { useState } from 'react';
import { AppWindow } from 'lucide-react';
import { useAppCategories, type OverrideRequest } from './useAppCategories';
import { AppCategoriesList } from './AppCategoriesList';
import { AppCategoryOverrideModal } from './AppCategoryOverrideModal';
import { AddAppCategoryModal } from './AddAppCategoryModal';
import { useNotifications } from '../../stores/uiStore';
import type { AppCategoryDisplayItem } from '../../types/appCategories';

interface AppCategoriesSectionProps {
  accessToken: string | null | undefined;
  orgId: string | null | undefined;
}

/**
 * Main section for managing app categories with override support
 */
export function AppCategoriesSection({ accessToken, orgId }: AppCategoriesSectionProps) {
  const { notify } = useNotifications();

  // State for modals
  const [editingApp, setEditingApp] = useState<AppCategoryDisplayItem | null>(null);
  const [isAddModalOpen, setIsAddModalOpen] = useState(false);

  // Hook for data and operations
  const {
    apps,
    overridesCount,
    uncategorizedCount,
    isLoading,
    error,
    searchQuery,
    activeFilter,
    setSearchQuery,
    setActiveFilter,
    createOverride,
    removeOverride,
  } = useAppCategories({ accessToken, orgId });

  /**
   * Handle creating/updating an override
   */
  const handleSaveOverride = async (request: OverrideRequest) => {
    await createOverride(request);
    notify.success(`${request.displayName} reclassificado como ${request.productivity === 'productive' ? 'Produtivo' : request.productivity === 'neutral' ? 'Neutro' : 'Distração'}`);
  };

  /**
   * Handle adding a new app classification
   */
  const handleAddApp = async (request: OverrideRequest) => {
    await createOverride(request);
    notify.success(`Classificação adicionada para ${request.identifier}`);
  };

  /**
   * Handle deleting an override
   */
  const handleDeleteOverride = async (identifier: string) => {
    // Confirmation
    if (!window.confirm(`Remover override? O app voltará à classificação global.`)) {
      return;
    }

    try {
      await removeOverride(identifier);
      notify.success('Override removido com sucesso');
    } catch (error) {
      notify.error('Erro ao remover override');
    }
  };

  /**
   * Handle editing an app
   */
  const handleEditApp = (app: AppCategoryDisplayItem) => {
    setEditingApp(app);
  };

  return (
    <div className="space-y-6">
      {/* Section Header */}
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-xl bg-[rgba(74,217,255,0.15)] flex items-center justify-center">
          <AppWindow className="w-5 h-5 text-[#4ad9ff]" />
        </div>
        <div>
          <h2 className="text-[18px] font-semibold text-[#f5f7fb]">Aplicativos</h2>
          <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
            Classifique apps e sites por produtividade
          </p>
        </div>
      </div>

      {/* Stats badges */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)]">
          <span className="text-[11px] text-[rgba(245,247,251,0.5)]">Apps detectados</span>
          <span className="text-[12px] font-medium text-[#f5f7fb]">{apps.length}</span>
        </div>
        {overridesCount > 0 && (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(74,217,255,0.1)] border border-[rgba(74,217,255,0.2)]">
            <span className="text-[11px] text-[#4ad9ff]">Overrides</span>
            <span className="text-[12px] font-medium text-[#4ad9ff]">{overridesCount}</span>
          </div>
        )}
      </div>

      {/* Main list */}
      <AppCategoriesList
        apps={apps}
        isLoading={isLoading}
        error={error}
        searchQuery={searchQuery}
        activeFilter={activeFilter}
        uncategorizedCount={uncategorizedCount}
        onSearchChange={setSearchQuery}
        onFilterChange={setActiveFilter}
        onEdit={handleEditApp}
        onDelete={handleDeleteOverride}
        onAdd={() => setIsAddModalOpen(true)}
      />

      {/* Override Modal */}
      <AppCategoryOverrideModal
        isOpen={editingApp !== null}
        onClose={() => setEditingApp(null)}
        onSave={handleSaveOverride}
        app={editingApp}
      />

      {/* Add Modal */}
      <AddAppCategoryModal
        isOpen={isAddModalOpen}
        onClose={() => setIsAddModalOpen(false)}
        onAdd={handleAddApp}
      />
    </div>
  );
}
