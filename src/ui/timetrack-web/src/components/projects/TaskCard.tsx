/**
 * TaskCard — single kanban card, draggable via @dnd-kit/sortable.
 * Click the card surface to open the detail drawer; the pencil button on hover
 * opens the edit modal (managers).
 * Click the assignee area to open an inline dropdown for quick reassignment.
 */

import { useTranslation } from 'react-i18next';
import { useSortable } from '@dnd-kit/sortable';
import { CSS } from '@dnd-kit/utilities';
import { Clock, Calendar, Pencil, PlayCircle, GripVertical } from 'lucide-react';
import type { Task, TaskPriority, TaskStatus } from '../../services/projectsApi';
import { AssigneeDropdown } from './AssigneeDropdown';

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

function priorityLabelKey(priority: TaskPriority): string {
  switch (priority) {
    case 'Urgent': return 'tasks.urgent';
    case 'High': return 'tasks.high';
    case 'Medium': return 'tasks.medium';
    case 'Low': return 'tasks.low';
    default: return 'tasks.none';
  }
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
  onEdit,
  onOpen,
  onAssigned,
}: {
  task: Task;
  projectColor: string;
  onEdit: (task: Task) => void;
  onOpen?: (taskId: string) => void;
  /** Called after assignment changes so the parent can refetch. */
  onAssigned?: () => void;
}) {
  const { t } = useTranslation();
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
      {...listeners}
      {...attributes}
      onClick={() => onOpen?.(task.id)}
      className="group relative bg-[rgba(26,29,46,0.8)] border border-[rgba(255,255,255,0.06)] rounded-lg p-3 cursor-grab active:cursor-grabbing hover:border-[rgba(255,255,255,0.12)] transition-colors"
    >
      {/* Grip icon — visual only, the whole card is the drag surface */}
      <span className="absolute top-1.5 right-6 p-0.5 rounded opacity-0 group-hover:opacity-100 text-[rgba(245,247,251,0.4)] pointer-events-none">
        <GripVertical className="w-3 h-3" />
      </span>

      {/* Edit button — stopPropagation only for click/pointer to avoid triggering card open */}
      <button
        onClick={(e) => {
          e.stopPropagation();
          onEdit(task);
        }}
        onPointerDown={(e) => e.stopPropagation()}
        className="absolute top-1.5 right-1.5 p-0.5 rounded opacity-0 group-hover:opacity-100 text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.06)] transition-all"
        title={t('tasks.edit')}
      >
        <Pencil className="w-3 h-3" />
      </button>

      {/* Running indicator stripe */}
      {task.isRunning && (
        <div className="flex items-center gap-1.5 mb-2 text-[9px] font-semibold text-[#05df72]">
          <PlayCircle className="w-3 h-3 animate-pulse" />
          {t('tasks.running')} {task.runningSeconds ? formatDuration(task.runningSeconds) : '0s'}
        </div>
      )}

      {/* Title + priority dot */}
      <div className="flex items-start gap-2 mb-2 pr-12">
        <span
          className="w-1.5 h-1.5 rounded-full flex-shrink-0 mt-1.5"
          style={{ backgroundColor: priorityColor(task.priority) }}
          title={`${t('tasks.priority')} ${t(priorityLabelKey(task.priority))}`}
        />
        <h4 className="flex-1 text-[12px] font-semibold text-[#f5f7fb] leading-snug">
          {task.title}
        </h4>
      </div>

      {task.description?.trim() && (
        <p className="text-[10px] text-[rgba(245,247,251,0.4)] line-clamp-3 whitespace-pre-wrap mb-2 pl-3.5">
          {task.description}
        </p>
      )}

      {/* Footer: assignee + worked time + due date */}
      <div className="flex items-center gap-2 pl-3.5 flex-wrap">
        <AssigneeDropdown
          task={task}
          projectColor={projectColor}
          onAssigned={onAssigned}
        />

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
                ? t('tasks.deadlineToday')
                : tone === 'overdue'
                ? t('tasks.overdue')
                : t('tasks.deadline')
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
