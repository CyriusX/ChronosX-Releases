/**
 * AddAppCategoryModal - Modal for adding a new app classification (CX-144)
 *
 * SOLID:
 * - SRP: Apenas UI de adição de classificação de app
 * - OCP: Extensível para novas opções
 *
 * Composition:
 * - Compõe Dialog + form controls
 */

import { useState, useEffect } from 'react';
import { X, Plus } from 'lucide-react';
import type { ProductivityCategory, AppSubcategory, IdentifierType } from '../../types/appCategories';
import { PRODUCTIVITY_CONFIG as productivityConfig, SUBCATEGORIES as subcategories, SUBCATEGORIES_BY_PRODUCTIVITY } from '../../types/appCategories';
import type { OverrideRequest } from './useAppCategories';

interface AddAppCategoryModalProps {
  isOpen: boolean;
  onClose: () => void;
  onAdd: (request: OverrideRequest) => Promise<void>;
}

/**
 * Modal for proactively adding app classifications
 */
export function AddAppCategoryModal({
  isOpen,
  onClose,
  onAdd,
}: AddAppCategoryModalProps) {
  const [identifier, setIdentifier] = useState('');
  const [identifierType, setIdentifierType] = useState<IdentifierType>('exe');
  const [productivity, setProductivity] = useState<ProductivityCategory>('productive');
  const [subcategory, setSubcategory] = useState<AppSubcategory>('productivity');
  const [note, setNote] = useState('');
  const [isSaving, setIsSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Reset form on close
  useEffect(() => {
    if (!isOpen) {
      setIdentifier('');
      setIdentifierType('exe');
      setProductivity('productive');
      setSubcategory('productivity');
      setNote('');
      setError(null);
    }
  }, [isOpen]);

  // Update subcategory when productivity changes
  useEffect(() => {
    const validSubcategories = SUBCATEGORIES_BY_PRODUCTIVITY[productivity] || [];
    if (!validSubcategories.includes(subcategory)) {
      setSubcategory(validSubcategories[0] || 'productivity');
    }
  }, [productivity, subcategory]);

  const handleAdd = async () => {
    // Validation
    if (!identifier.trim()) {
      setError('Nome do executável ou domínio é obrigatório');
      return;
    }

    setIsSaving(true);
    setError(null);

    try {
      await onAdd({
        identifier: identifier.trim(),
        identifierType,
        displayName: identifier.trim(),
        productivity,
        subcategory,
        note: note.trim() || undefined,
      });
      onClose();
    } catch (err) {
      console.error('[AddAppCategoryModal] Error adding:', err);
      setError(err instanceof Error ? err.message : 'Erro ao adicionar classificação');
    } finally {
      setIsSaving(false);
    }
  };

  if (!isOpen) return null;

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
          <div className="flex items-center gap-3">
            <div className="w-8 h-8 rounded-lg bg-[rgba(74,217,255,0.15)] flex items-center justify-center">
              <Plus className="w-4 h-4 text-[#4ad9ff]" />
            </div>
            <h2 className="text-[16px] font-semibold text-[#f5f7fb]">
              Adicionar classificação
            </h2>
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
          {/* Identifier */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Nome do executável ou domínio:
            </label>
            <input
              type="text"
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
              placeholder="ex: meuapp.exe ou meusite.com.br"
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2.5 text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4ad9ff]"
            />
          </div>

          {/* Type */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Tipo:
            </label>
            <div className="grid grid-cols-2 gap-2">
              <button
                onClick={() => setIdentifierType('exe')}
                className={`px-3 py-2 rounded-lg text-[12px] font-medium transition-all ${
                  identifierType === 'exe'
                    ? 'bg-[rgba(74,217,255,0.15)] text-[#f5f7fb] border border-[rgba(74,217,255,0.3)]'
                    : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
                }`}
              >
                App desktop
              </button>
              <button
                onClick={() => setIdentifierType('domain')}
                className={`px-3 py-2 rounded-lg text-[12px] font-medium transition-all ${
                  identifierType === 'domain'
                    ? 'bg-[rgba(74,217,255,0.15)] text-[#f5f7fb] border border-[rgba(74,217,255,0.3)]'
                    : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
                }`}
              >
                Site/Domínio
              </button>
            </div>
          </div>

          {/* Productivity */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Classificação:
            </label>
            <div className="grid grid-cols-3 gap-2">
              {(['productive', 'neutral', 'distraction'] as ProductivityCategory[]).map(
                (cat) => (
                  <button
                    key={cat}
                    onClick={() => setProductivity(cat)}
                    className={`px-3 py-2.5 rounded-lg text-[12px] font-medium transition-all ${
                      productivity === cat
                        ? 'bg-[rgba(74,217,255,0.15)] text-[#f5f7fb] border border-[rgba(74,217,255,0.3)]'
                        : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)]'
                    }`}
                  >
                    {productivityConfig[cat].emoji} {productivityConfig[cat].labelPt}
                  </button>
                )
              )}
            </div>
          </div>

          {/* Subcategory */}
          <div>
            <label className="text-[12px] text-[rgba(245,247,251,0.5)] mb-2 block">
              Subcategoria:
            </label>
            <select
              value={subcategory}
              onChange={(e) => setSubcategory(e.target.value as AppSubcategory)}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2.5 text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[#4ad9ff]"
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
              rows={2}
              className="w-full bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] rounded-lg px-3 py-2 text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4ad9ff] resize-none"
            />
          </div>

          {/* Error */}
          {error && (
            <div className="bg-[rgba(255,107,107,0.1)] border border-[rgba(255,107,107,0.2)] rounded-lg p-3">
              <p className="text-[12px] text-[#ff6b6b]">{error}</p>
            </div>
          )}
        </div>

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
            onClick={handleAdd}
            disabled={isSaving}
            className="px-4 py-2 rounded-lg text-[13px] font-medium bg-[rgba(5,223,114,0.15)] text-[#05df72] border border-[rgba(5,223,114,0.3)] hover:bg-[rgba(5,223,114,0.25)] transition-colors disabled:opacity-50"
          >
            {isSaving ? 'Adicionando...' : 'Adicionar'}
          </button>
        </div>
      </div>
    </div>
  );
}
