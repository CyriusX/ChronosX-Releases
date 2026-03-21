/**
 * ProjectCard - Card component for displaying a project
 */

import { Archive, Edit2, MoreVertical, RotateCcw, Trash2 } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { Project } from '../../stores/projectStore';
import { scaleIn, TIMING } from '../../lib/animation';

interface ProjectCardProps {
  project: Project;
  isMenuOpen: boolean;
  onToggleMenu: () => void;
  onEdit: () => void;
  onArchive: () => void;
  onReactivate: () => void;
  onDelete: () => void;
  isArchived: boolean;
}

export function ProjectCard({
  project,
  isMenuOpen,
  onToggleMenu,
  onEdit,
  onArchive,
  onReactivate,
  onDelete,
  isArchived,
}: ProjectCardProps) {
  return (
    <motion.div
      className="bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4 hover:border-[rgba(255,255,255,0.1)] transition-[border-color,box-shadow] relative"
      whileHover={{ y: -4, boxShadow: '0 8px 30px rgba(0,0,0,0.3)' }}
      transition={{ duration: TIMING.fast }}
    >
      <div
        className="absolute top-0 left-4 w-12 h-1 rounded-b-full"
        style={{ backgroundColor: project.color }}
      />

      <div className="flex items-start justify-between mt-2">
        <div className="flex-1">
          <h3 className="font-medium text-[#f5f7fb] mb-1">{project.name}</h3>
          {project.description && (
            <p className="text-sm text-[rgba(245,247,251,0.5)] line-clamp-2">
              {project.description}
            </p>
          )}
          <p className="text-xs text-[rgba(245,247,251,0.3)] mt-2">
            Criado em {new Date(project.createdAt).toLocaleDateString('pt-BR')}
          </p>
        </div>

        <div className="relative">
          <button
            onClick={onToggleMenu}
            className="p-1 hover:bg-[rgba(255,255,255,0.05)] rounded-lg transition-colors"
          >
            <MoreVertical className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
          </button>

          <AnimatePresence>
            {isMenuOpen && (
              <motion.div
                className="absolute right-0 top-full mt-1 w-40 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-lg shadow-lg z-10"
                variants={scaleIn}
                initial="hidden"
                animate="visible"
                exit="exit"
                transition={{ duration: TIMING.fast }}
              >
                {!isArchived ? (
                  <>
                    <button
                      onClick={onEdit}
                      className="w-full flex items-center gap-2 px-3 py-2 text-sm text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.05)] transition-colors"
                    >
                      <Edit2 className="w-4 h-4" />
                      Editar
                    </button>
                    <button
                      onClick={onArchive}
                      className="w-full flex items-center gap-2 px-3 py-2 text-sm text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.05)] transition-colors"
                    >
                      <Archive className="w-4 h-4" />
                      Arquivar
                    </button>
                  </>
                ) : (
                  <button
                    onClick={onReactivate}
                    className="w-full flex items-center gap-2 px-3 py-2 text-sm text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.05)] transition-colors"
                  >
                    <RotateCcw className="w-4 h-4" />
                    Reativar
                  </button>
                )}
                <button
                  onClick={onDelete}
                  className="w-full flex items-center gap-2 px-3 py-2 text-sm text-[#FF6B7A] hover:bg-[rgba(255,107,122,0.1)] transition-colors"
                >
                  <Trash2 className="w-4 h-4" />
                  Excluir
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </motion.div>
  );
}
