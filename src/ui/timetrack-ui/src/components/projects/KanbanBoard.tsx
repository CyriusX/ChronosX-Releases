/**
 * KanbanBoard (desktop) — 3-column drag-and-drop kanban.
 *
 * Only allows moving cards assigned to the current user. Drops from the
 * user's own column call the backend's /tasks/{id}/move endpoint. A 409
 * triggers a full refetch (the card was modified by someone else).
 */

import { useState, useMemo, useRef } from 'react';
import {
  DndContext,
  DragEndEvent,
  DragOverlay,
  DragStartEvent,
  PointerSensor,
  useSensor,
  useSensors,
  closestCenter,
} from '@dnd-kit/core';
import { SortableContext, verticalListSortingStrategy, useSortable } from '@dnd-kit/sortable';
import { ListTodo, CircleDashed, CheckCircle2, Eye, Plus, Loader2 } from 'lucide-react';
import { TaskCard } from './TaskCard';
import { TaskDetailDrawer } from './TaskDetailDrawer';
import { moveTask, createTask, type Task, type TaskStatus, type ProjectSyncSource } from '../../services/projectsApi';

type ColumnDef = {
  id: TaskStatus;
  label: string;
  icon: React.ComponentType<{ className?: string }>;
  accent: string;
};

const LOCAL_COLUMNS: ColumnDef[] = [
  { id: 'Todo', label: 'A Fazer', icon: ListTodo, accent: '#94a3b8' },
  { id: 'InProgress', label: 'Em Progresso', icon: CircleDashed, accent: '#fbbf24' },
  { id: 'Done', label: 'Concluído', icon: CheckCircle2, accent: '#05df72' },
];

const LINEAR_COLUMNS: ColumnDef[] = [
  { id: 'Todo', label: 'A Fazer', icon: ListTodo, accent: '#94a3b8' },
  { id: 'InProgress', label: 'Em Progresso', icon: CircleDashed, accent: '#fbbf24' },
  { id: 'InReview', label: 'Em Revisão', icon: Eye, accent: '#a855f7' },
  { id: 'Done', label: 'Concluído', icon: CheckCircle2, accent: '#05df72' },
];

function formatTotalWorked(seconds: number): string {
  if (seconds < 60) return '0m';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h === 0) return `${m}m`;
  return `${h}h ${m}m`;
}

export function KanbanBoard({
  tasks,
  projectId,
  projectColor,
  currentUserId,
  syncSource = 'Local',
  onLocalChange,
  onConflict,
  onTaskCreated,
  onTaskDeleted,
}: {
  tasks: Task[];
  projectId: string;
  projectColor: string;
  currentUserId: string | null | undefined;
  /** 'Linear' unlocks the extra "Em Revisão" column. Defaults to 'Local'. */
  syncSource?: ProjectSyncSource;
  onLocalChange: (tasks: Task[]) => void;
  onConflict: () => void;
  onTaskCreated?: (task: Task) => void;
  onTaskDeleted?: (taskId: string) => void;
}) {
  const [activeTask, setActiveTask] = useState<Task | null>(null);
  const [drawerTaskId, setDrawerTaskId] = useState<string | null>(null);
  const [addingTask, setAddingTask] = useState(false);
  const [newTaskTitle, setNewTaskTitle] = useState('');
  const [creatingTask, setCreatingTask] = useState(false);
  const newTaskInputRef = useRef<HTMLInputElement>(null);

  const COLUMNS = syncSource === 'Linear' ? LINEAR_COLUMNS : LOCAL_COLUMNS;

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
  );

  const columns = useMemo(() => {
    const byStatus: Record<TaskStatus, Task[]> = { Todo: [], InProgress: [], InReview: [], Done: [] };
    tasks.forEach((t) => byStatus[t.status]?.push(t));
    (Object.keys(byStatus) as TaskStatus[]).forEach((k) =>
      byStatus[k].sort((a, b) => a.position - b.position),
    );
    return byStatus;
  }, [tasks]);

  const isMine = (task: Task) => !!currentUserId && task.assignedUserId === currentUserId;

  const startAddingTask = () => {
    setAddingTask(true);
    setNewTaskTitle('');
    // Focus the input on next paint
    setTimeout(() => newTaskInputRef.current?.focus(), 50);
  };

  const cancelAddingTask = () => {
    setAddingTask(false);
    setNewTaskTitle('');
  };

  const submitNewTask = async () => {
    const title = newTaskTitle.trim();
    if (!title || creatingTask) return;
    setCreatingTask(true);
    try {
      const task = await createTask(projectId, { title });
      onTaskCreated?.(task);
      setAddingTask(false);
      setNewTaskTitle('');
    } catch (err) {
      console.error('[KanbanBoard] createTask failed', err);
    } finally {
      setCreatingTask(false);
    }
  };

  const onDragStart = (event: DragStartEvent) => {
    const task = event.active.data.current?.task as Task | undefined;
    if (task && isMine(task)) setActiveTask(task);
  };

  const onDragEnd = async (event: DragEndEvent) => {
    setActiveTask(null);
    const { active, over } = event;
    if (!over) return;

    const activeTask = active.data.current?.task as Task | undefined;
    if (!activeTask || !isMine(activeTask)) return;

    let targetStatus: TaskStatus | null = null;
    let targetIndex = -1;

    if (over.data.current?.type === 'column') {
      targetStatus = over.data.current.status as TaskStatus;
      targetIndex = columns[targetStatus].length;
    } else if (over.data.current?.type === 'task') {
      const overTask = over.data.current.task as Task;
      targetStatus = overTask.status;
      targetIndex = columns[targetStatus].findIndex((t) => t.id === overTask.id);
    }

    if (targetStatus === null || targetIndex < 0) return;

    // Defensive: InReview only on Linear-synced projects
    if (targetStatus === 'InReview' && syncSource !== 'Linear') return;

    const currentIndex = columns[activeTask.status].findIndex((t) => t.id === activeTask.id);
    if (targetStatus === activeTask.status && currentIndex === targetIndex) return;

    const targetColumn = columns[targetStatus].filter((t) => t.id !== activeTask.id);
    const before = targetColumn[targetIndex - 1];
    const after = targetColumn[targetIndex];
    let newPosition: number;
    if (!before && !after) newPosition = 1024;
    else if (!before) newPosition = after!.position - 1024;
    else if (!after) newPosition = before.position + 1024;
    else newPosition = (before.position + after.position) / 2;

    // Optimistic update
    const optimistic = tasks.map((t) =>
      t.id === activeTask.id ? { ...t, status: targetStatus!, position: newPosition } : t,
    );
    onLocalChange(optimistic);

    try {
      await moveTask(activeTask.id, {
        status: targetStatus,
        position: newPosition,
        rowVersion: activeTask.rowVersion,
      });
    } catch (err) {
      console.error('[KanbanBoard] move failed', err);
      onConflict();
    }
  };

  return (
    <DndContext sensors={sensors} collisionDetection={closestCenter} onDragStart={onDragStart} onDragEnd={onDragEnd}>
      <div className="flex gap-3 h-full overflow-x-auto pb-2">
        {COLUMNS.map((col) => {
          const colTasks = columns[col.id];
          const Icon = col.icon;
          const totalWorked = colTasks.reduce((sum, t) => sum + t.totalSecondsWorked, 0);

          const isTodoCol = col.id === 'Todo';
          const canAddTask = isTodoCol && syncSource !== 'Linear';

          return (
            <KanbanColumn
              key={col.id}
              id={col.id}
              label={col.label}
              icon={<Icon className="w-3.5 h-3.5" />}
              accent={col.accent}
              count={colTasks.length}
              totalWorked={totalWorked}
              onAdd={canAddTask ? startAddingTask : undefined}
            >
              <SortableContext items={colTasks.map((t) => t.id)} strategy={verticalListSortingStrategy}>
                <div className="space-y-2">
                  {canAddTask && addingTask && (
                    <div className="p-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.1)] space-y-2">
                      <input
                        ref={newTaskInputRef}
                        value={newTaskTitle}
                        onChange={(e) => setNewTaskTitle(e.target.value)}
                        onKeyDown={(e) => {
                          if (e.key === 'Enter') submitNewTask();
                          if (e.key === 'Escape') cancelAddingTask();
                        }}
                        placeholder="Nome da tarefa…"
                        className="w-full bg-transparent text-[11px] text-[#f5f7fb] placeholder:text-[rgba(245,247,251,0.3)] outline-none"
                      />
                      <div className="flex gap-1.5">
                        <button
                          onClick={submitNewTask}
                          disabled={!newTaskTitle.trim() || creatingTask}
                          className="flex items-center gap-1 px-2 py-1 rounded text-[10px] font-medium bg-[#4A9FFF] text-white disabled:opacity-40 disabled:cursor-not-allowed hover:bg-[#3b8fee] transition-colors"
                        >
                          {creatingTask ? <Loader2 className="w-3 h-3 animate-spin" /> : null}
                          Adicionar
                        </button>
                        <button
                          onClick={cancelAddingTask}
                          className="px-2 py-1 rounded text-[10px] text-[rgba(245,247,251,0.5)] hover:text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
                        >
                          Cancelar
                        </button>
                      </div>
                    </div>
                  )}
                  {colTasks.map((task) => (
                    <TaskCard
                      key={task.id}
                      task={task}
                      projectColor={projectColor}
                      draggable={isMine(task)}
                      onOpen={setDrawerTaskId}
                    />
                  ))}
                  {colTasks.length === 0 && !addingTask && (
                    <div className="text-center py-8 text-[10px] text-[rgba(245,247,251,0.25)] italic">
                      Sem tarefas
                    </div>
                  )}
                </div>
              </SortableContext>
            </KanbanColumn>
          );
        })}
      </div>

      <DragOverlay>
        {activeTask ? (
          <div className="rotate-2 opacity-90">
            <TaskCard task={activeTask} projectColor={projectColor} draggable={false} />
          </div>
        ) : null}
      </DragOverlay>

      <TaskDetailDrawer
        taskId={drawerTaskId}
        onClose={() => setDrawerTaskId(null)}
        onDeleted={(id) => {
          setDrawerTaskId(null);
          onTaskDeleted?.(id);
        }}
      />
    </DndContext>
  );
}

function KanbanColumn({
  id,
  label,
  icon,
  accent,
  count,
  totalWorked,
  onAdd,
  children,
}: {
  id: TaskStatus;
  label: string;
  icon: React.ReactNode;
  accent: string;
  count: number;
  totalWorked: number;
  onAdd?: () => void;
  children: React.ReactNode;
}) {
  const { setNodeRef } = useSortable({
    id: `col-${id}`,
    data: { type: 'column', status: id },
  });

  return (
    <div
      ref={setNodeRef}
      className="w-[280px] flex-shrink-0 bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.05)] rounded-xl flex flex-col max-h-full"
    >
      <div className="px-3 py-2.5 flex items-center gap-2 border-b border-[rgba(255,255,255,0.04)] flex-shrink-0">
        <span
          className="flex items-center justify-center w-6 h-6 rounded-md"
          style={{ backgroundColor: `${accent}20`, color: accent }}
        >
          {icon}
        </span>
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span className="text-[11px] font-semibold text-[#f5f7fb]">{label}</span>
            <span className="text-[10px] text-[rgba(245,247,251,0.4)]">· {count}</span>
          </div>
          {totalWorked > 0 && (
            <div className="text-[9px] text-[rgba(245,247,251,0.35)]">
              {formatTotalWorked(totalWorked)} trabalhado
            </div>
          )}
        </div>
        {onAdd && (
          <button
            onClick={onAdd}
            className="p-1 rounded text-[rgba(245,247,251,0.4)] hover:text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.06)] transition-colors flex-shrink-0"
            aria-label="Adicionar tarefa"
            title="Adicionar tarefa"
          >
            <Plus className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      <div className="flex-1 overflow-y-auto p-2 min-h-[100px]">{children}</div>
    </div>
  );
}
