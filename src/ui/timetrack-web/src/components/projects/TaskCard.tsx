/**
 * TaskCard — single kanban card, draggable via @dnd-kit/sortable.
 */

import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Clock, User, Calendar, Pencil, PlayCircle } from 'lucide-react';
import type { Task } from '../../services/projectsApi';

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  return `${h}h ${m % 60}m`;
}

function priorityColor(priority: string): string {
  switch (priority) {
    case 'High': return '#f87171';
    case 'Medium': return '#fbbf24';
    case 'Low': return '#60a5fa';
    default: return '#94a3b8';
  }
}

function initials(name: string | null | undefined): string {
  if (!name) return '?';
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0][0]?.toUpperCase() ?? '?';
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function TaskCard({
  task,
  projectColor,
  onEdit,
}: {
  task: Task;
  projectColor: string;
  onEdit: (task: Task) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: task.id,
    data: { type: 'task', task },
  });

  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.4 : 1,
  };

  const worked = task.isRunning && task.runningSeconds
    ? task.totalSecondsWorked + task.runningSeconds
    : task.totalSecondsWorked;

  const dueDate = task.dueDate ? new Date(task.dueDate) : null;
  const isOverdue = dueDate && dueDate < new Date() && task.status !== 'Done';

  return (
    <div
      ref={setNodeRef}
      style={style}
      {...attributes}
      {...listeners}
      className="group bg-[rgba(26,29,46,0.8)] border border-[rgba(255,255,255,0.06)] rounded-lg p-3 cursor-grab active:cursor-grabbing hover:border-[rgba(255,255,255,0.12)] transition-colors"
    >
      {/* Running indicator stripe */}
      {task.isRunning && (
        <div className="flex items-center gap-1.5 mb-2 text-[9px] font-semibold text-[#05df72]">
          <PlayCircle className="w-3 h-3 animate-pulse" />
          EM EXECUÇÃO · {task.runningSeconds ? formatDuration(task.runningSeconds) : '0s'}
        </div>
      )}

      {/* Title + priority dot */}
      <div className="flex items-start gap-2 mb-2">
        <span
          className="w-1.5 h-1.5 rounded-full flex-shrink-0 mt-1.5"
          style={{ backgroundColor: priorityColor(task.priority) }}
          title={`Prioridade: ${task.priority}`}
        />
        <h4 className="flex-1 text-[12px] font-semibold text-[#f5f7fb] leading-snug">
          {task.title}
        </h4>
        <button
          onClick={(e) => {
            e.stopPropagation();
            onEdit(task);
          }}
          onPointerDown={(e) => e.stopPropagation()}
          className="opacity-0 group-hover:opacity-100 p-0.5 rounded text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.06)] transition-all"
          title="Editar"
        >
          <Pencil className="w-3 h-3" />
        </button>
      </div>

      {task.description && (
        <p className="text-[10px] text-[rgba(245,247,251,0.4)] line-clamp-2 mb-2 pl-3.5">
          {task.description}
        </p>
      )}

      {/* Footer: assignee + worked time + due date */}
      <div className="flex items-center gap-2 pl-3.5 flex-wrap">
        {task.assignedUserDisplayName ? (
          <div
            className="flex items-center gap-1 text-[9px] text-[rgba(245,247,251,0.6)]"
            title={task.assignedUserDisplayName}
          >
            <div
              className="w-4 h-4 rounded-full flex items-center justify-center text-[8px] font-bold text-white"
              style={{ backgroundColor: projectColor }}
            >
              {initials(task.assignedUserDisplayName)}
            </div>
            <span className="truncate max-w-[80px]">{task.assignedUserDisplayName.split(' ')[0]}</span>
          </div>
        ) : (
          <div className="flex items-center gap-1 text-[9px] text-[rgba(245,247,251,0.3)]">
            <User className="w-3 h-3" />
            Não atribuído
          </div>
        )}

        {worked > 0 && (
          <div className="flex items-center gap-1 text-[9px] text-[rgba(245,247,251,0.45)]">
            <Clock className="w-3 h-3" />
            {formatDuration(worked)}
          </div>
        )}

        {dueDate && (
          <div
            className={`flex items-center gap-1 text-[9px] ${
              isOverdue ? 'text-[#f87171]' : 'text-[rgba(245,247,251,0.45)]'
            }`}
          >
            <Calendar className="w-3 h-3" />
            {dueDate.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })}
          </div>
        )}
      </div>
    </div>
  );
}
