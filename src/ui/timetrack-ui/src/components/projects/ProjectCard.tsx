/**
 * ProjectCard — premium project card with stats (task counts, time worked, billable cost).
 */

import { Archive, Edit2, MoreVertical, RotateCcw, Trash2, Clock, CheckCircle2, CircleDashed, ListTodo, DollarSign, Zap } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { Project } from '../../stores/projectStore';
import { scaleIn, TIMING } from '../../lib/animation';

export interface ProjectStats {
  todoCount: number;
  inProgressCount: number;
  doneCount: number;
  totalSecondsWorked: number;
}

interface ProjectCardProps {
  project: Project;
  stats?: ProjectStats;
  isMenuOpen: boolean;
  onToggleMenu: () => void;
  onEdit: () => void;
  onArchive: () => void;
  onReactivate: () => void;
  onDelete: () => void;
  isArchived: boolean;
}

function fmtDuration(s: number): string {
  if (s <= 0) return '0m';
  const h = Math.floor(s / 3600);
  const m = Math.floor((s % 3600) / 60);
  if (h > 0 && m > 0) return `${h}h ${m}m`;
  if (h > 0) return `${h}h`;
  return `${m}m`;
}

export function ProjectCard({
  project,
  stats,
  isMenuOpen,
  onToggleMenu,
  onEdit,
  onArchive,
  onReactivate,
  onDelete,
  isArchived,
}: ProjectCardProps) {
  const totalTasks = (stats?.todoCount ?? 0) + (stats?.inProgressCount ?? 0) + (stats?.doneCount ?? 0);
  const totalCost = project.isBillable && project.hourlyRate && stats
    ? ((stats.totalSecondsWorked / 3600) * project.hourlyRate)
    : null;

  return (
    <motion.div
      className="group relative bg-[rgba(17,19,28,0.7)] border border-[rgba(255,255,255,0.07)] rounded-2xl overflow-hidden transition-[border-color,box-shadow]"
      whileHover={{
        y: -3,
        borderColor: `${project.color}40`,
        boxShadow: `0 12px 40px rgba(0,0,0,0.4), 0 0 0 1px ${project.color}25`,
      }}
      transition={{ duration: TIMING.fast }}
    >
      {/* Color bar top */}
      <div
        className="h-[3px] w-full"
        style={{ background: `linear-gradient(90deg, ${project.color}, ${project.color}80)` }}
      />

      {/* Subtle color glow in background */}
      <div
        className="absolute inset-0 opacity-0 group-hover:opacity-100 transition-opacity duration-300 pointer-events-none"
        style={{ background: `radial-gradient(ellipse at top left, ${project.color}0a 0%, transparent 60%)` }}
      />

      <div className="p-4 flex flex-col gap-3 relative">
        {/* Header: name + menu */}
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-center gap-2 min-w-0 flex-1">
            <span
              className="w-2.5 h-2.5 rounded-full flex-shrink-0"
              style={{ backgroundColor: project.color, boxShadow: `0 0 0 2px #11131c, 0 0 0 3px ${project.color}60` }}
            />
            <h3 className="font-semibold text-[14px] text-[#f5f7fb] truncate leading-tight">
              {project.name}
            </h3>
            {project.isBillable && (
              <span className="flex-shrink-0 flex items-center gap-0.5 px-1.5 py-0.5 rounded-full bg-[rgba(196,181,253,0.1)] border border-[rgba(196,181,253,0.2)] text-[9px] font-semibold text-[#c4b5fd]">
                <DollarSign className="w-2.5 h-2.5" />
                {project.currency ?? 'USD'}
              </span>
            )}
          </div>

          <div className="relative flex-shrink-0">
            <button
              onClick={(e) => { e.stopPropagation(); onToggleMenu(); }}
              className="p-1 rounded-lg text-[rgba(245,247,251,0.35)] hover:text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
            >
              <MoreVertical className="w-4 h-4" />
            </button>

            <AnimatePresence>
              {isMenuOpen && (
                <motion.div
                  className="absolute right-0 top-full mt-1 w-44 bg-[#1a1d2e] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl z-20 overflow-hidden"
                  variants={scaleIn}
                  initial="hidden"
                  animate="visible"
                  exit="exit"
                  transition={{ duration: TIMING.fast }}
                >
                  {!isArchived ? (
                    <>
                      <button
                        onClick={(e) => { e.stopPropagation(); onEdit(); }}
                        className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[12px] text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.05)] hover:text-[#f5f7fb] transition-colors"
                      >
                        <Edit2 className="w-3.5 h-3.5" />
                        Editar projeto
                      </button>
                      <button
                        onClick={(e) => { e.stopPropagation(); onArchive(); }}
                        className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[12px] text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.05)] hover:text-[#f5f7fb] transition-colors"
                      >
                        <Archive className="w-3.5 h-3.5" />
                        Arquivar
                      </button>
                    </>
                  ) : (
                    <button
                      onClick={(e) => { e.stopPropagation(); onReactivate(); }}
                      className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[12px] text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.05)] hover:text-[#f5f7fb] transition-colors"
                    >
                      <RotateCcw className="w-3.5 h-3.5" />
                      Reativar
                    </button>
                  )}
                  <div className="border-t border-[rgba(255,255,255,0.06)]" />
                  <button
                    onClick={(e) => { e.stopPropagation(); onDelete(); }}
                    className="w-full flex items-center gap-2.5 px-3.5 py-2.5 text-[12px] text-[#f87171] hover:bg-[rgba(248,113,113,0.08)] transition-colors"
                  >
                    <Trash2 className="w-3.5 h-3.5" />
                    Excluir
                  </button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>

        {/* Description */}
        {project.description ? (
          <p className="text-[11px] text-[rgba(245,247,251,0.45)] line-clamp-2 leading-relaxed -mt-1">
            {project.description}
          </p>
        ) : (
          <p className="text-[11px] text-[rgba(245,247,251,0.2)] italic -mt-1">Sem descrição</p>
        )}

        {/* Task status chips */}
        {stats && totalTasks > 0 && (
          <div className="flex items-center gap-1.5 flex-wrap">
            {stats.todoCount > 0 && (
              <span className="flex items-center gap-1 px-2 py-1 rounded-lg bg-[rgba(148,163,184,0.1)] border border-[rgba(148,163,184,0.15)] text-[10px] text-[#94a3b8]">
                <ListTodo className="w-3 h-3" />
                {stats.todoCount} a fazer
              </span>
            )}
            {stats.inProgressCount > 0 && (
              <span className="flex items-center gap-1 px-2 py-1 rounded-lg bg-[rgba(251,191,36,0.1)] border border-[rgba(251,191,36,0.15)] text-[10px] text-[#fbbf24]">
                <CircleDashed className="w-3 h-3" />
                {stats.inProgressCount} em progresso
              </span>
            )}
            {stats.doneCount > 0 && (
              <span className="flex items-center gap-1 px-2 py-1 rounded-lg bg-[rgba(5,223,114,0.1)] border border-[rgba(5,223,114,0.15)] text-[10px] text-[#05df72]">
                <CheckCircle2 className="w-3 h-3" />
                {stats.doneCount} concluído
              </span>
            )}
          </div>
        )}

        {stats && totalTasks === 0 && (
          <div className="flex items-center gap-1.5">
            <span className="text-[10px] text-[rgba(245,247,251,0.25)] italic">Sem tarefas ainda</span>
          </div>
        )}

        {/* Divider */}
        <div className="border-t border-[rgba(255,255,255,0.05)]" />

        {/* Bottom stats row */}
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-center gap-3">
            {/* Time worked */}
            <div className="flex items-center gap-1 text-[10px] text-[rgba(245,247,251,0.45)]">
              <Clock className="w-3 h-3" />
              {stats ? fmtDuration(stats.totalSecondsWorked) : '—'}
            </div>

            {/* Billable cost */}
            {totalCost !== null && totalCost > 0 && (
              <div className="flex items-center gap-1 text-[10px] font-semibold text-[#c4b5fd]">
                <Zap className="w-3 h-3" />
                {project.currency ?? 'USD'} {totalCost.toFixed(2)}
              </div>
            )}
          </div>

          {/* Created date */}
          <span className="text-[10px] text-[rgba(245,247,251,0.25)]">
            {new Date(project.createdAt).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' })}
          </span>
        </div>
      </div>
    </motion.div>
  );
}
