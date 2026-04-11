/**
 * TaskDetailDrawer (desktop) — right-side sheet showing full task detail.
 * Opens when a kanban card is clicked. Fetches /tasks/{id} on open.
 */

import { useEffect, useState } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  X,
  Clock,
  Calendar,
  User,
  Tag,
  Flag,
  PlayCircle,
  AlertTriangle,
  Lock,
  CheckCircle2,
  CircleDashed,
  ListTodo,
  Eye,
  Trash2,
  Pencil,
  Save,
  XCircle,
} from 'lucide-react';
import { getTask, deleteTask, updateTask, type Task, type TaskStatus, type TaskPriority } from '../../services/projectsApi';
import { SimpleMarkdown } from './SimpleMarkdown';
import { useAuthStore, selectUser } from '../../stores';

// ── helpers ────────────────────────────────────────────────────────────────

function formatDuration(seconds: number): string {
  if (seconds <= 0) return '0m';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  if (h > 0) return `${h}h ${m}m`;
  if (m > 0) return `${m}m ${s}s`;
  return `${s}s`;
}

function formatDateTime(iso: string | null | undefined): string {
  if (!iso) return '—';
  return new Date(iso).toLocaleString('pt-BR', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '—';
  const datePart = iso.split('T')[0];
  return new Date(datePart + 'T00:00:00').toLocaleDateString('pt-BR', { day: '2-digit', month: 'short', year: 'numeric' });
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

function statusMeta(status: TaskStatus) {
  switch (status) {
    case 'Todo': return { label: 'A Fazer', color: '#94a3b8', Icon: ListTodo };
    case 'InProgress': return { label: 'Em Progresso', color: '#fbbf24', Icon: CircleDashed };
    case 'InReview': return { label: 'Em Revisão', color: '#a855f7', Icon: Eye };
    case 'Done': return { label: 'Concluído', color: '#05df72', Icon: CheckCircle2 };
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
  // Parse as local midnight to avoid UTC-offset shifting the date
  const datePart = dueDate.split('T')[0];
  const d = new Date(datePart + 'T00:00:00');
  const now = new Date();
  const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate());
  const startOfDue = new Date(d.getFullYear(), d.getMonth(), d.getDate());
  if (startOfDue.getTime() < startOfToday.getTime()) return 'overdue';
  if (startOfDue.getTime() === startOfToday.getTime()) return 'today';
  return 'future';
}

// ── component ──────────────────────────────────────────────────────────────

export function TaskDetailDrawer({
  taskId,
  onClose,
  onDeleted,
}: {
  taskId: string | null;
  onClose: () => void;
  onDeleted?: (taskId: string) => void;
}) {
  const currentUser = useAuthStore(selectUser);
  const [task, setTask] = useState<Task | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState(false);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [editTitle, setEditTitle] = useState('');
  const [editDescription, setEditDescription] = useState('');
  const [editPriority, setEditPriority] = useState<TaskPriority>('Medium');
  const [editDueDate, setEditDueDate] = useState('');

  const startEditing = () => {
    if (!task) return;
    setEditTitle(task.title);
    setEditDescription(task.description ?? '');
    setEditPriority(task.priority);
    setEditDueDate(task.dueDate ? new Date(task.dueDate).toISOString().split('T')[0] : '');
    setEditing(true);
    setError(null);
  };

  const cancelEditing = () => { setEditing(false); setError(null); };

  const handleSave = async () => {
    if (!task || saving) return;
    const title = editTitle.trim();
    if (!title) return;
    setSaving(true);
    try {
      const updated = await updateTask(task.id, {
        title,
        description: editDescription.trim() || null,
        assignedUserId: task.assignedUserId ?? null,
        priority: editPriority,
        dueDate: editDueDate ? new Date(editDueDate + 'T12:00:00').toISOString() : null,
      });
      setTask(updated);
      setEditing(false);
    } catch (err) {
      console.error('[TaskDetailDrawer] update failed', err);
      setError('Não foi possível salvar as alterações.');
    } finally {
      setSaving(false);
    }
  };

  useEffect(() => {
    if (!taskId) {
      setTask(null);
      setError(null);
      setEditing(false);
      return;
    }
    let cancelled = false;
    setLoading(true);
    setError(null);
    getTask(taskId)
      .then((t) => {
        if (!cancelled) setTask(t);
      })
      .catch((err) => {
        console.error('[TaskDetailDrawer] failed to load task', err);
        if (!cancelled) setError('Não foi possível carregar a tarefa.');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [taskId]);

  useEffect(() => {
    if (!taskId) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [taskId, onClose]);

  const handleDelete = async () => {
    if (!task || deleting) return;
    if (!window.confirm(`Excluir tarefa "${task.title}"?`)) return;
    setDeleting(true);
    try {
      await deleteTask(task.id);
      onDeleted?.(task.id);
      onClose();
    } catch (err) {
      console.error('[TaskDetailDrawer] delete failed', err);
      setError('Não foi possível excluir a tarefa.');
    } finally {
      setDeleting(false);
    }
  };

  const canEdit = task !== null
    && !task.isLinearSourced
    && currentUser !== null
    && task.createdByUserId === currentUser.id;

  const canDelete = canEdit;

  const open = taskId !== null;

  return (
    <AnimatePresence>
      {open && (
        <>
          <motion.div
            className="fixed inset-0 bg-black/40 z-40"
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.15 }}
            onClick={onClose}
          />
          <motion.aside
            className="fixed right-0 top-0 bottom-0 w-full md:w-[480px] z-50 bg-[#0b0d14] border-l border-[rgba(255,255,255,0.08)] shadow-2xl overflow-y-auto"
            initial={{ x: '100%' }}
            animate={{ x: 0 }}
            exit={{ x: '100%' }}
            transition={{ type: 'spring', stiffness: 260, damping: 30 }}
          >
            <DrawerBody
              task={task}
              loading={loading}
              error={error}
              onClose={onClose}
              canEdit={canEdit}
              canDelete={canDelete}
              deleting={deleting}
              onDelete={handleDelete}
              editing={editing}
              saving={saving}
              editTitle={editTitle}
              editDescription={editDescription}
              editPriority={editPriority}
              editDueDate={editDueDate}
              onStartEdit={startEditing}
              onCancelEdit={cancelEditing}
              onSave={handleSave}
              onEditTitle={setEditTitle}
              onEditDescription={setEditDescription}
              onEditPriority={setEditPriority}
              onEditDueDate={setEditDueDate}
            />
          </motion.aside>
        </>
      )}
    </AnimatePresence>
  );
}

function DrawerBody({
  task,
  loading,
  error,
  onClose,
  canEdit,
  canDelete,
  deleting,
  onDelete,
  editing,
  saving,
  editTitle,
  editDescription,
  editPriority,
  editDueDate,
  onStartEdit,
  onCancelEdit,
  onSave,
  onEditTitle,
  onEditDescription,
  onEditPriority,
  onEditDueDate,
}: {
  task: Task | null;
  loading: boolean;
  error: string | null;
  onClose: () => void;
  canEdit: boolean;
  canDelete: boolean;
  deleting: boolean;
  onDelete: () => void;
  editing: boolean;
  saving: boolean;
  editTitle: string;
  editDescription: string;
  editPriority: TaskPriority;
  editDueDate: string;
  onStartEdit: () => void;
  onCancelEdit: () => void;
  onSave: () => void;
  onEditTitle: (v: string) => void;
  onEditDescription: (v: string) => void;
  onEditPriority: (v: TaskPriority) => void;
  onEditDueDate: (v: string) => void;
}) {
  if (loading || !task) {
    return (
      <>
        <DrawerHeader title="Carregando…" onClose={onClose} />
        {error && (
          <div className="p-6 text-[12px] text-[#f87171]">{error}</div>
        )}
      </>
    );
  }

  const status = statusMeta(task.status);
  const worked = task.isRunning && task.runningSeconds
    ? task.totalSecondsWorked + task.runningSeconds
    : task.totalSecondsWorked;
  const tone = deadlineTone(task.dueDate, task.status);

  return (
    <>
      <DrawerHeader title={task.projectName} onClose={onClose} projectColor={task.projectColor} />

      <div className="px-6 pt-4 pb-8 space-y-5">
        {/* Running banner */}
        {task.isRunning && (
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(5,223,114,0.08)] border border-[rgba(5,223,114,0.2)]">
            <PlayCircle className="w-4 h-4 text-[#05df72] animate-pulse" />
            <span className="text-[11px] font-semibold text-[#05df72]">
              EM EXECUÇÃO · {formatDuration(task.runningSeconds ?? 0)}
            </span>
          </div>
        )}

        {/* Deadline banner */}
        {tone === 'today' && (
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(251,191,36,0.08)] border border-[rgba(251,191,36,0.25)]">
            <AlertTriangle className="w-4 h-4 text-[#fbbf24]" />
            <span className="text-[11px] font-semibold text-[#fbbf24]">
              Prazo é hoje — {formatDate(task.dueDate)}
            </span>
          </div>
        )}
        {tone === 'overdue' && (
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(239,68,68,0.08)] border border-[rgba(239,68,68,0.25)]">
            <AlertTriangle className="w-4 h-4 text-[#f87171]" />
            <span className="text-[11px] font-semibold text-[#f87171]">
              Atrasada — prazo era {formatDate(task.dueDate)}
            </span>
          </div>
        )}

        {/* Error (edit mode) */}
        {error && (
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)] text-[11px] text-[#f87171]">
            {error}
          </div>
        )}

        {/* Title — view or edit */}
        {editing ? (
          <div className="space-y-3">
            <div>
              <label className="block text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.45)] mb-1.5">Título</label>
              <input
                value={editTitle}
                onChange={(e) => onEditTitle(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) onSave(); if (e.key === 'Escape') onCancelEdit(); }}
                className="w-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.1)] rounded-lg px-3 py-2 text-[14px] font-semibold text-[#f5f7fb] outline-none focus:border-[rgba(74,159,255,0.5)] transition-colors"
                autoFocus
              />
            </div>
            <div>
              <label className="block text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.45)] mb-1.5">Descrição</label>
              <textarea
                value={editDescription}
                onChange={(e) => onEditDescription(e.target.value)}
                onKeyDown={(e) => { if (e.key === 'Escape') onCancelEdit(); }}
                rows={4}
                className="w-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.1)] rounded-lg px-3 py-2 text-[12px] text-[rgba(245,247,251,0.8)] outline-none focus:border-[rgba(74,159,255,0.5)] transition-colors resize-none"
                placeholder="Descrição (opcional)…"
              />
            </div>
            <div className="grid grid-cols-2 gap-3">
              <div>
                <label className="block text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.45)] mb-1.5">Prioridade</label>
                <select
                  value={editPriority}
                  onChange={(e) => onEditPriority(e.target.value as TaskPriority)}
                  className="w-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.1)] rounded-lg px-3 py-2 text-[12px] text-[#f5f7fb] outline-none focus:border-[rgba(74,159,255,0.5)] transition-colors"
                >
                  <option value="None">Nenhuma</option>
                  <option value="Low">Baixa</option>
                  <option value="Medium">Média</option>
                  <option value="High">Alta</option>
                  <option value="Urgent">Urgente</option>
                </select>
              </div>
              <div>
                <label className="block text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.45)] mb-1.5">Prazo</label>
                <input
                  type="date"
                  value={editDueDate}
                  onChange={(e) => onEditDueDate(e.target.value)}
                  className="w-full bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.1)] rounded-lg px-3 py-2 text-[12px] text-[#f5f7fb] outline-none focus:border-[rgba(74,159,255,0.5)] transition-colors"
                />
              </div>
            </div>
            <div className="flex gap-2 pt-1">
              <button
                onClick={onSave}
                disabled={!editTitle.trim() || saving}
                className="flex-1 flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg bg-[#4A9FFF] text-white text-[12px] font-semibold hover:bg-[#3b8fee] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
              >
                <Save className="w-3.5 h-3.5" />
                {saving ? 'Salvando…' : 'Salvar'}
              </button>
              <button
                onClick={onCancelEdit}
                className="px-4 py-2.5 rounded-lg border border-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.6)] text-[12px] hover:bg-[rgba(255,255,255,0.05)] transition-colors"
              >
                <XCircle className="w-3.5 h-3.5" />
              </button>
            </div>
          </div>
        ) : (
          <div className="flex items-start gap-2">
            <h2 className="flex-1 text-[18px] font-semibold text-[#f5f7fb] leading-tight">{task.title}</h2>
            {canEdit && (
              <button
                onClick={onStartEdit}
                className="p-1.5 rounded-lg text-[rgba(245,247,251,0.4)] hover:text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.06)] transition-colors flex-shrink-0"
                title="Editar tarefa"
              >
                <Pencil className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        )}

        {/* Meta grid — hidden while editing */}
        {!editing && (<>
        <div className="grid grid-cols-2 gap-3">
          <MetaField icon={<status.Icon className="w-3 h-3" />} label="Status" accent={status.color}>
            {status.label}
          </MetaField>
          <MetaField icon={<Flag className="w-3 h-3" />} label="Prioridade" accent={priorityColor(task.priority)}>
            {priorityLabel(task.priority)}
          </MetaField>
          <MetaField icon={<User className="w-3 h-3" />} label="Atribuído">
            {task.assignedUserDisplayName ? (
              <div className="flex items-center gap-1.5">
                <div
                  className="w-4 h-4 rounded-full flex items-center justify-center text-[7px] font-bold text-white"
                  style={{ backgroundColor: task.projectColor }}
                >
                  {initials(task.assignedUserDisplayName)}
                </div>
                <span className="truncate">{task.assignedUserDisplayName}</span>
              </div>
            ) : (
              <span className="text-[rgba(245,247,251,0.4)]">Ninguém</span>
            )}
          </MetaField>
          <MetaField icon={<Calendar className="w-3 h-3" />} label="Prazo">
            {!task.dueDate ? (
              <span className="text-[rgba(245,247,251,0.4)]">—</span>
            ) : tone === 'today' ? (
              <span className="text-[#fbbf24] font-semibold">Prazo hoje</span>
            ) : tone === 'overdue' ? (
              <span className="text-[#f87171] font-semibold">Atrasado · {formatDate(task.dueDate)}</span>
            ) : (
              <span>{formatDate(task.dueDate)}</span>
            )}
          </MetaField>
        </div>

        {/* Time worked */}
        <div className="px-4 py-3 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          <div className="flex items-center gap-2 mb-1.5">
            <Clock className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
            <span className="text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.5)]">
              Tempo trabalhado
            </span>
          </div>
          <div className="text-[20px] font-semibold text-[#f5f7fb] font-mono tabular-nums">
            {formatDuration(worked)}
          </div>
          {task.movedToInProgressAt && (
            <div className="text-[10px] text-[rgba(245,247,251,0.4)] mt-1">
              Iniciada em {formatDateTime(task.movedToInProgressAt)}
            </div>
          )}
        </div>

        {/* Description */}
        <div>
          <div className="flex items-center gap-2 mb-2">
            <Tag className="w-3 h-3 text-[rgba(245,247,251,0.5)]" />
            <span className="text-[10px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.5)]">
              Descrição
            </span>
          </div>
          {task.description?.trim() ? (
            <SimpleMarkdown source={task.description} />
          ) : (
            <p className="text-[11px] italic text-[rgba(245,247,251,0.35)]">Sem descrição.</p>
          )}
        </div>

        {/* Timestamps */}
        <div className="pt-3 border-t border-[rgba(255,255,255,0.06)] space-y-1.5 text-[10px] text-[rgba(245,247,251,0.45)]">
          <div className="flex justify-between">
            <span>Criada</span>
            <span>{formatDateTime(task.createdAt)}</span>
          </div>
          {task.updatedAt && (
            <div className="flex justify-between">
              <span>Atualizada</span>
              <span>{formatDateTime(task.updatedAt)}</span>
            </div>
          )}
          {task.completedAt && (
            <div className="flex justify-between">
              <span>Concluída</span>
              <span>{formatDateTime(task.completedAt)}</span>
            </div>
          )}
        </div>
        {/* end of !editing block */}
        </>)}

        {/* Actions — only in view mode */}
        {!editing && (canDelete ? (
          <button
            onClick={onDelete}
            disabled={deleting}
            className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-lg border border-[rgba(239,68,68,0.3)] text-[#f87171] text-[11px] font-medium hover:bg-[rgba(239,68,68,0.08)] hover:border-[rgba(239,68,68,0.5)] disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
          >
            <Trash2 className="w-3.5 h-3.5" />
            {deleting ? 'Excluindo…' : 'Excluir tarefa'}
          </button>
        ) : (
          <div className="flex items-center gap-2 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.05)]">
            <Lock className="w-3 h-3 text-[rgba(245,247,251,0.35)]" />
            <span className="text-[10px] text-[rgba(245,247,251,0.45)]">
              Somente leitura — edição disponível apenas para gestores na web.
            </span>
          </div>
        ))}
      </div>
    </>
  );
}

function DrawerHeader({
  title,
  onClose,
  projectColor,
}: {
  title: string;
  onClose: () => void;
  projectColor?: string;
}) {
  return (
    <div className="sticky top-0 z-10 bg-[#0b0d14] border-b border-[rgba(255,255,255,0.06)] px-6 py-4 flex items-center justify-between">
      <div className="flex items-center gap-2 min-w-0">
        {projectColor && (
          <span
            className="w-2 h-2 rounded-full flex-shrink-0"
            style={{ backgroundColor: projectColor }}
          />
        )}
        <span className="text-[11px] font-semibold uppercase tracking-wider text-[rgba(245,247,251,0.6)] truncate">
          {title}
        </span>
      </div>
      <button
        onClick={onClose}
        className="p-1 rounded text-[rgba(245,247,251,0.5)] hover:text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
        aria-label="Fechar"
      >
        <X className="w-4 h-4" />
      </button>
    </div>
  );
}

function MetaField({
  icon,
  label,
  accent,
  children,
}: {
  icon: React.ReactNode;
  label: string;
  accent?: string;
  children: React.ReactNode;
}) {
  return (
    <div className="px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.05)]">
      <div className="flex items-center gap-1.5 text-[9px] uppercase tracking-wider text-[rgba(245,247,251,0.45)] mb-1">
        <span style={accent ? { color: accent } : undefined}>{icon}</span>
        {label}
      </div>
      <div className="text-[11px] text-[#f5f7fb] truncate" style={accent ? { color: accent } : undefined}>
        {children}
      </div>
    </div>
  );
}
