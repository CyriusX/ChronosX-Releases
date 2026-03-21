/**
 * ProjectModal - Modal for creating/editing a project
 */

import { useState } from 'react';
import { motion } from 'motion/react';
import { Project } from '../../stores/projectStore';
import { PROJECT_COLORS } from './projectConstants';
import { modalOverlayVariants, modalContentVariants, SPRING, TIMING } from '../../lib/animation';

interface ProjectModalProps {
  project: Project | null;
  onClose: () => void;
  onSubmit: (name: string, description: string, color: string) => Promise<void>;
  isLoading: boolean;
}

export function ProjectModal({ project, onClose, onSubmit, isLoading }: ProjectModalProps) {
  const [name, setName] = useState(project?.name || '');
  const [description, setDescription] = useState(project?.description || '');
  const [color, setColor] = useState(project?.color || PROJECT_COLORS[0]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    await onSubmit(name.trim(), description.trim(), color);
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
          {project ? 'Editar Projeto' : 'Novo Projeto'}
        </h2>

        <form onSubmit={handleSubmit}>
          <div className="space-y-4">
            {/* Name */}
            <div>
              <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-1">
                Nome do Projeto
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
                Descricao (opcional)
              </label>
              <textarea
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                className="w-full px-3 py-2 bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.1)] rounded-lg text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[#4A9FFF] resize-none"
                placeholder="Descreva o projeto..."
                rows={3}
              />
            </div>

            {/* Color */}
            <div>
              <label className="block text-sm font-medium text-[rgba(245,247,251,0.8)] mb-2">
                Cor
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
          </div>

          {/* Actions */}
          <div className="flex justify-end gap-3 mt-6">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-[rgba(245,247,251,0.6)] hover:text-[#f5f7fb] transition-colors"
            >
              Cancelar
            </button>
            <motion.button
              type="submit"
              disabled={isLoading || !name.trim()}
              className="px-4 py-2 bg-gradient-to-r from-[#4A9FFF] to-[#3C7BFF] text-white rounded-lg font-medium text-sm hover:opacity-90 transition-opacity disabled:opacity-50"
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
            >
              {isLoading ? 'Salvando...' : project ? 'Salvar' : 'Criar Projeto'}
            </motion.button>
          </div>
        </form>
      </motion.div>
    </motion.div>
  );
}
