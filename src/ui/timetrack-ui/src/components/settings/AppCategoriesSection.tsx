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
import { useTranslation } from 'react-i18next';
import { AppWindow } from 'lucide-react';
import { useAppCategories, type OverrideRequest } from './useAppCategories';
import { AppCategoriesList } from './AppCategoriesList';
import { AppCategoryOverrideModal } from './AppCategoryOverrideModal';
import { AddAppCategoryModal } from './AddAppCategoryModal';
import { useNotifications } from '../../stores/uiStore';
import type { AppCategoryDisplayItem } from '../../types/appCategories';

interface AppCategoriesSectionProps {
  orgId: string | null | undefined;
}

/**
 * Main section for managing app categories with override support
 */
export function AppCategoriesSection({ orgId }: AppCategoriesSectionProps) {
  const { t } = useTranslation();
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
  } = useAppCategories({ orgId });

  /**
   * Handle creating/updating an override
   */
  const handleSaveOverride = async (request: OverrideRequest) => {
    try {
      await createOverride(request);
      const categoryLabel = request.productivity === 'productive' ? t('policies.appCategories.productive') : request.productivity === 'neutral' ? t('policies.appCategories.neutral') : t('policies.appCategories.distraction');
      notify.success(t('policies.appCategories.reclassified', { name: request.displayName, category: categoryLabel }));
    } catch (error) {
      const message = error instanceof Error ? error.message : 'unknown';
      notify.error(t('policies.appCategories.classificationSaveError', { message }));
      throw error; // Re-throw so the modal can also display the error
    }
  };

  /**
   * Handle adding a new app classification
   */
  const handleAddApp = async (request: OverrideRequest) => {
    try {
      await createOverride(request);
      notify.success(t('policies.appCategories.classificationAdded', { identifier: request.identifier }));
    } catch (error) {
      const message = error instanceof Error ? error.message : 'unknown';
      notify.error(t('policies.appCategories.classificationAddError', { message }));
      throw error;
    }
  };

  /**
   * Handle deleting an override
   */
  const handleDeleteOverride = async (identifier: string) => {
    // Confirmation
    if (!window.confirm(t('policies.appCategories.removeOverrideConfirm'))) {
      return;
    }

    try {
      await removeOverride(identifier);
      notify.success(t('policies.appCategories.overrideRemoved'));
    } catch (error) {
      notify.error(t('policies.appCategories.overrideRemoveError'));
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
        <div className="w-10 h-10 rounded-xl bg-[rgba(139,92,246,0.15)] flex items-center justify-center">
          <AppWindow className="w-5 h-5 text-[#8B5CF6]" />
        </div>
        <div>
          <h2 className="text-[18px] font-semibold text-[#f5f7fb]">{t('policies.appCategories.appsTitle')}</h2>
          <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
            {t('policies.appCategories.appsSubtitle')}
          </p>
        </div>
      </div>

      {/* Stats badges */}
      <div className="flex flex-wrap items-center gap-3">
        <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)]">
          <span className="text-[11px] text-[rgba(245,247,251,0.5)]">{t('policies.appCategories.detectedApps')}</span>
          <span className="text-[12px] font-medium text-[#f5f7fb]">{apps.length}</span>
        </div>
        {overridesCount > 0 && (
          <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg bg-[rgba(139,92,246,0.1)] border border-[rgba(139,92,246,0.2)]">
            <span className="text-[11px] text-[#8B5CF6]">{t('policies.appCategories.overrides')}</span>
            <span className="text-[12px] font-medium text-[#8B5CF6]">{overridesCount}</span>
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
