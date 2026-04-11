/**
 * TaskCard (desktop) — single kanban card. Draggable only when the current
 * user is the assignee. Other cards are visible but locked. Click the card
 * surface to open the detail drawer.
 */

import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Clock, User, Calendar, PlayCircle, Lock, GripVertical } from 'lucide-react';
import type { Task, TaskPriority, TaskStatus } from '../../services/projectsApi';

function formatDuration(seconds: number): string {
  if (seconds < 60) return `${seconds}s`;
  const m = Math.floor(seconds / 60);
  if (m < 60) return `${m}m`;
  const h = Math.floor(m / 60);
  return `${h}h ${m % 60}m`;
}

function priorityColor(priority: TaskPriority): string {
  switch (priority) {
    case 'Urgent': return '#ef4444';
    case 'High': return '#fb923c';
    case 'Medium': return '#fbbf24';
    case 'Low': return '#60a5fa';
    default: return '#94a3b8';
  }
}

function priorityLabel(priority: TaskPriority): string {
  switch (priority) {
    case 'Urgent': return 'Urgente';
    case 'High': return 'Alta';
    case 'Medium': return 'Média';
    case 'Low': return 'Baixa';
    default: return 'Nenhuma';
  }
}

function initials(name: string | null | undefined): string {
  if (!name) return '?';
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0][0]?.toUpperCase() ?? '?';
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function deadlineTone(dueDate: string | null, status: TaskStatus): 'overdue' | 'today' | 'future' | 'none' {
  if (!dueDate || status === 'Done') return 'none';
  const d = new Date(dueDate);
  const now = new Date();
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const startOfDue = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  if (startOfDue.getTime() < startOfToday.getTime()) return 'overdue';
  if (startOfDue.getTime() === startOfToday.getTime()) return 'today';
  return 'future';
}

export function TaskCard({
  task,
  projectColor,
  draggable,
  onOpen,
}: {
  task: Task;
  projectColor: string;
  /** True when the current user can move this card (= assignee). */
  draggable: boolean;
  /** Called when the user clicks the card surface. */
  onOpen?: (taskId: string) => void;
}) {
  const { attributes, listeners, setNodeRef, transform, transition, isDragging } = useSortable({
    id: task.id,
    data: { type: 'task', task },
    disabled: !draggable,
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
  const tone = deadlineTone(task.dueDate, task.status);

  const deadlineClasses =
    tone === 'overdue'
      ? 'bg-[rgba(239,68,68,0.12)] text-[#f87171] border-[rgba(239,68,68,0.25)]'
      : tone === 'today'
      ? 'bg-[rgba(251,191,36,0.12)] text-[#fbbf24] border-[rgba(251,191,36,0.25)]'
      : 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.5)] border-[rgba(255,255,255,0.06)]';

  return (
    <div
      ref={setNodeRef}
      style={style}
      className={`group relative bg-[rgba(26,29,46,0.8)] border border-[rgba(255,255,255,0.06)] rounded-lg p-3 transition-colors ${
        draggable
          ? 'hover:border-[rgba(255,255,255,0.12)] cursor-pointer'
          : 'cursor-pointer opacity-75'
      }`}
      onClick={() => onOpen?.(task.id)}
    >
      {/* Drag handle — only when the user owns the card */}
      {draggable && (
        <button
          {...attributes}
          {...listeners}
          onClick={(e) => e.stopPropagation()}
          className="absolute top-1.5 right-1.5 p-0.5 rounded opacity-0 group-hover:opacity-100 text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.06)] transition-all cursor-grab active:cursor-grabbing"
          aria-label="Arrastar"
          title="Arrastar"
        >
          <GripVertical className="w-3 h-3" />
        </button>
      )}

      {/* Running indicator */}
      {task.isRunning && (
        <div className="flex items-center gap-1.5 mb-2 text-[9px] font-semibold text-[#05df72]">
          <PlayCircle className="w-3 h-3 animate-pulse" />
          EM EXECUÇÃO · {task.runningSeconds ? formatDuration(task.runningSeconds) : '0s'}
        </div>
      )}

      {/* Title + priority dot + lock */}
      <div className="flex items-start gap-2 mb-2 pr-5">
        <span
          className="w-1.5 h-1.5 rounded-full flex-shrink-0 mt-1.5"
          style={{ backgroundColor: priorityColor(task.priority) }}
          title={`Prioridade: ${priorityLabel(task.priority)}`}
        />
        <h4 className="flex-1 text-[12px] font-semibold text-[#f5f7fb] leading-snug">
          {task.title}
        </h4>
        {!draggable && (
          <Lock
            className="w-3 h-3 text-[rgba(245,247,251,0.25)] flex-shrink-0 mt-0.5"
            aria-label="Não é sua tarefa"
          />
        )}
      </div>

      {task.description?.trim() && (
        <p className="text-[10px] text-[rgba(245,247,251,0.4)] line-clamp-3 whitespace-pre-wrap mb-2 pl-3.5">
          {task.description}
        </p>
      )}

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
            className={`flex items-center gap-1 text-[9px] px-1.5 py-0.5 rounded border ${deadlineClasses}`}
            title={
              tone === 'today'
                ? 'Prazo é hoje'
                : tone === 'overdue'
                ? 'Atrasada'
                : 'Prazo'
            }
          >
            <Calendar className="w-3 h-3" />
            {dueDate.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })}
          </div>
        )}
      </div>
    </div>
  );
}
