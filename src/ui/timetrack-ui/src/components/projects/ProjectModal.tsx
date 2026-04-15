/**
 * ProjectModal - Modal for creating/editing a project
 */

import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { DollarSign } from 'lucide-react';
import { Project } from '../../stores/projectStore';
import { PROJECT_COLORS } from './projectConstants';
import { modalOverlayVariants, modalContentVariants, SPRING, TIMING } from '../../lib/animation';

interface ProjectModalProps {
  project: Project | null;
  onClose: () => void;
  onSubmit: (name: string, description: string, color: string, isBillable: boolean, currency: string | null, hourlyRate: number | null) => Promise<void>;
  isLoading: boolean;
}

export function ProjectModal({ project, onClose, onSubmit, isLoading }: ProjectModalProps) {
  const { t } = useTranslation();
  const [name, setName] = useState(project?.name || '');
  const [description, setDescription] = useState(project?.description || '');
  const [color, setColor] = useState(project?.color || PROJECT_COLORS[0]);
  const [isBillable, setIsBillable] = useState(project?.isBillable || false);
  const [currency, setCurrency] = useState(project?.currency || 'USD');
  const [hourlyRate, setHourlyRate] = useState(project?.hourlyRate?.toString() || '');

  const CURRENCIES = ['USD', 'BRL', 'EUR', 'GBP', 'CAD', 'AUD', 'JPY'] as const;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    if (isBillable && (!currency.trim() || !hourlyRate.trim() || parseFloat(hourlyRate) <= 0)) return;
    await onSubmit(name.trim(), description.trim(), color, isBillable, isBillable ? currency : null, isBillable ? parseFloat(hourlyRate) : null);
  };

  return (
    <motion.div
      className="fixed inset-0 bg-black/50 flex items-center justify-center z-50"
      variants={modalOverlayVariants}
      initial="hidden"
      animate="visible"
      exit="exit"
      transition={{ duration: TIMING.fast }}
      onClick={onClose}
    >
      <motion.div
        className="bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl w-full max-w-md p-6"
        variants={modalContentVariants}
        initial="hidden"
        animate="visible"
        exit="exit"
        transition={SPRING.gentle}
        onClick={(e) => e.stopPropagation()}
      >
        <h2 className="text-xl font-semibold text-[#f5f7fb] mb-4">
          {project ? t('projects.editProject') : t('projects.newProject')}
        </h2>

        <form onSubmit={handleSubmit}>
          <div className="space-y-4">
            {/* Name */}
            <div>
              <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-1">
                {t('projects.projectName')}
              </label>
              <input
                type="text"
                value={name}
                onChange={(e) => setName(e.target.value)}
                className="w-full px-3 py-2 bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.1)] rounded-lg text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4A9FFF]"
                placeholder="Ex: Website Redesign"
                required
              />
            </div>

            {/* Description */}
            <div>
              <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-1">
                {t('projects.descriptionOptional')}
              </label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full px-3 py-2 bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.1)] rounded-lg text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4A9FFF] resize-none"
                placeholder={t('projects.descriptionPlaceholder')}
                rows={3}
              />
            </div>

            {/* Color */}
            <div>
              <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-2">
                {t('projects.color')}
              </label>
              <div className="flex flex-wrap gap-2">
                {PROJECT_COLORS.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => setColor(c)}
                    className={`w-8 h-8 rounded-full transition-transform ${
                      color === c ? 'ring-2 ring-white ring-offset-2 ring-offset-[#0b0d14] scale-110' : ''
                    }`}
                    style={{ backgroundColor: c }}
                  />
                ))}
              </div>
            </div>

            {/* Billable Toggle */}
            <div className="flex items-center justify-between py-2">
              <label className="flex items-center gap-2 text-sm font-medium text-[rgba(245,247,251,0.8)] cursor-pointer">
                <div className="relative">
                  <input
                    type="checkbox"
                    checked={isBillable}
                    onChange={(e) => setIsBillable(e.target.checked)}
                    className="sr-only peer"
                  />
                  <div className={`w-9 h-5 rounded-full transition-colors ${
                    isBillable ? 'bg-[#4A9FFF]' : 'bg-[rgba(255,255,255,0.1)]'
                  }`}>
                    <div className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full transition-all duration-200 ${
                      isBillable ? 'translate-x-4 bg-white' : 'translate-x-0 bg-[rgba(255,255,255,0.4)]'
                    }`} />
                  </div>
                </div>
                <span>{t('projects.billable')}</span>
              </label>
            </div>

            {/* Billable Fields */}
            {isBillable && (
              <>
                {/* Currency */}
                <div>
                  <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-2">
                    {t('projects.currency')}
                  </label>
                  <div className="grid grid-cols-4 gap-2">
                    {CURRENCIES.map((c) => (
                      <button
                        key={c}
                        type="button"
                        onClick={() => setCurrency(c)}
                        className={`py-2 px-3 rounded-lg text-sm font-semibold transition-all ${
                          currency === c
                            ? 'bg-gradient-to-r from-[#4A9FFF] to-[#3C7BFF] text-white'
                            : 'bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.9)]'
                        }`}
                      >
                        {c}
                      </button>
                    ))}
                  </div>
                </div>

                {/* Hourly Rate */}
                <div>
                  <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-1">
                    {t('projects.hourlyRate')}
                  </label>
                  <div className="relative">
                    <div className="absolute left-3 top-1/2 -translate-y-1/2 flex items-center text-[rgba(245,247,251,0.4)]">
                      <DollarSign className="w-4 h-4" />
                    </div>
                    <input
                      type="number"
                      step="0.01"
                      min="0"
                      value={hourlyRate}
                      onChange={(e) => setHourlyRate(e.target.value)}
                      className="w-full pl-9 pr-3 py-2 bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.1)] rounded-lg text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4A9FFF]"
                      placeholder="40.00"
                    />
                  </div>
                </div>
              </>
            )}
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-[rgba(245,247,251,0.6)] hover:text-[#f5f7fb] transition-colors"
            >
              {t('common.cancel')}
            </button>
            <motion.button
              type="submit"
              disabled={isLoading || !name.trim() || (isBillable && (!currency.trim() || !hourlyRate.trim() || parseFloat(hourlyRate) <= 0))}
              className="px-4 py-2 bg-gradient-to-r from-[#4A9FFF] to-[#3C7BFF] text-white rounded-lg font-medium text-sm hover:opacity-90 transition-opacity disabled:opacity-50"
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
            >
              {isLoading ? t('common.saving') : project ? t('common.save') : t('projects.createProject')}
            </motion.button>
          </div>
        </form>
      </motion.div>
    </motion.div>
  );
}
