/**
 * AppCategoryOverrideModal - Modal for editing/creating app category overrides (CX-144)
 *
 * SOLID:
 * - SRP: Apenas UI de edição de classificação de app
 * - OCP: Extensível para novas opções de classificação
 *
 * Composition:
 * - Compõe Dialog + form controls
 */

import { useState, useEffect } from 'react';
import { X, AlertCircle } from 'lucide-react';
import type {
  ProductivityCategory,
  AppSubcategory,
  IdentifierType,
} from '../../types/appCategories';
import { PRODUCTIVITY_CONFIG as productivityConfig, SUBCATEGORIES as subcategories, SUBCATEGORIES_BY_PRODUCTIVITY } from '../../types/appCategories';
import type { OverrideRequest } from './useAppCategories';

interface AppCategoryOverrideModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSave: (request: OverrideRequest) => Promise<void>;
  app: {
    identifier: string;
    identifierType: IdentifierType;
    displayName: string;
    productivity: ProductivityCategory;
    subcategory: AppSubcategory;
    source: string;
    note?: string;
  } | null;
}

/**
 * Modal for creating/editing app category overrides
 */
export function AppCategoryOverrideModal({
  isOpen,
  onClose,
  onSave,
  app,
}: AppCategoryOverrideModalProps) {
  const [productivity, setProductivity] = useState<ProductivityCategory>('neutral');
  const [subcategory, setSubcategory] = useState<AppSubcategory>('utilities');
  const [note, setNote] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);

  // Reset form when app changes
  useEffect(() => {
    if (app) {
      setProductivity(app.productivity === 'unknown' ? 'neutral' : app.productivity);
      setSubcategory(app.subcategory === 'unknown' ? 'utilities' : app.subcategory);
      setNote(app.note || '');
      setSaveError(null);
    }
  }, [app]);

  // Update subcategory when productivity changes
  useEffect(() => {
    const validSubcategories = SUBCATEGORIES_BY_PRODUCTIVITY[productivity] || [];
    if (!validSubcategories.includes(subcategory)) {
      setSubcategory(validSubcategories[0] || 'utilities');
    }
  }, [productivity, subcategory]);

  const handleSave = async () => {
    if (!app) return;

    setIsSaving(true);
    setSaveError(null);
    try {
      await onSave({
        identifier: app.identifier,
        identifierType: app.identifierType,
        displayName: app.displayName,
        productivity,
        subcategory,
        note: note.trim() || undefined,
      });
      onClose();
    } catch (error) {
      console.error('[AppCategoryOverrideModal] Error saving:', error);
      const message = error instanceof Error ? error.message : 'Erro desconhecido ao salvar';
      setSaveError(message);
    } finally {
      setIsSaving(false);
    }
  };

  if (!isOpen || !app) return null;

  const availableSubcategories = SUBCATEGORIES_BY_PRODUCTIVITY[productivity] || [];

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center">
      {/* Backdrop */}
      <div
        className="absolute inset-0 bg-black/60 backdrop-blur-sm"
        onClick={onClose}
      />

      {/* Modal */}
      <div className="relative w-full max-w-md bg-[#1a1d2e] border border-[rgba(255,255,255,0.08)] rounded-2xl shadow-xl">
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-[rgba(255,255,255,0.06)]">
          <div>
            <h2 className="text-[16px] font-semibold text-[#f5f7fb]">
              Classificar: {app.displayName}
            </h2>
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
              {app.identifier}
            </p>
          </div>
          <button
            onClick={onClose}
            className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
          >
            <X className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
          </button>
        </div>

        {/* Content */}
        <div className="p-5 space-y-5">
          {/* Current classification */}
          {app.source === 'global' && app.productivity !== 'unknown' && (
            <div className="bg-[rgba(255,255,255,0.02)] rounded-lg p-3">
              <p className="text-[11px] text-[rgba(245,247,251,0.4)] mb-1">
                Classificação atual (global):
              </p>
              <p className="text-[13px] text-[rgba(245,247,251,0.7)]">
                {productivityConfig[app.productivity].emoji} {productivityConfig[app.productivity].labelPt} — {subcategories[app.subcategory].labelPt}
              </p>
            </div>
          )}

          {/* Productivity selection */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Nova classificação:
            </label>
            <div className="grid grid-cols-3 gap-2">
              {(['productive', 'neutral', 'distraction'] as ProductivityCategory[]).map(
                (cat) => (
                  <button
                    key={cat}
                    onClick={() => setProductivity(cat)}
                    className={`px-3 py-2.5 rounded-lg text-[12px] font-medium transition-all ${
                      productivity === cat
                        ? 'bg-[rgba(139,92,246,0.15)] text-[#f5f7fb] border border-[rgba(139,92,246,0.3)]'
                        : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
                    }`}
                  >
                    {productivityConfig[cat].emoji} {productivityConfig[cat].labelPt}
                  </button>
                )
              )}
            </div>
          </div>

          {/* Subcategory selection */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Subcategoria:
            </label>
            <select
              value={subcategory}
              onChange={(e) => setSubcategory(e.target.value as AppSubcategory)}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2.5 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#8B5CF6]"
            >
              {availableSubcategories.map((sub) => (
                <option key={sub} value={sub}>
                  {subcategories[sub].labelPt}
                </option>
              ))}
            </select>
          </div>

          {/* Note */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Nota (opcional):
            </label>
            <textarea
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="Justificativa para a classificação..."
              rows={3}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#8B5CF6] resize-none"
            />
          </div>

          {/* Warning */}
          <div className="flex items-start gap-2 bg-[rgba(255,193,7,0.1)] border border-[rgba(255,193,7,0.2)] rounded-lg p-3">
            <AlertCircle className="w-4 h-4 text-[#FFC107] flex-shrink-0 mt-0.5" />
            <p className="text-[11px] text-[rgba(245,247,251,0.6)]">
              Esta mudança afeta toda a organização.
            </p>
          </div>
        </div>

        {/* Error message */}
        {saveError && (
          <div className="mx-5 mb-0 flex items-start gap-2 bg-[rgba(248,113,113,0.1)] border border-[rgba(248,113,113,0.2)] rounded-lg p-3">
            <AlertCircle className="w-4 h-4 text-[#f87171] flex-shrink-0 mt-0.5" />
            <p className="text-[11px] text-[#f87171]">{saveError}</p>
          </div>
        )}

        {/* Footer */}
        <div className="flex items-center justify-end gap-3 p-5 border-t border-[rgba(255,255,255,0.06)]">
          <button
            onClick={onClose}
            disabled={isSaving}
            className="px-4 py-2 rounded-lg text-[13px] font-medium text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.8)] transition-colors disabled:opacity-50"
          >
            Cancelar
          </button>
          <button
            onClick={handleSave}
            disabled={isSaving}
            className="px-4 py-2 rounded-lg text-[13px] font-medium bg-[rgba(139,92,246,0.15)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)] hover:bg-[rgba(139,92,246,0.25)] transition-colors disabled:opacity-50"
          >
            {isSaving ? 'Salvando...' : 'Salvar override'}
          </button>
        </div>
      </div>
    </div>
  );
}
