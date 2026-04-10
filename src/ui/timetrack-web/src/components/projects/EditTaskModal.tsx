/**
 * EditTaskModal — edit an existing task (title, description, assignee, priority, due date).
 * Also supports soft-deleting the task.
 */

import { useState } from 'react';
import { motion } from 'motion/react';
import { X, Loader2, Trash2 } from 'lucide-react';
import { updateTask, deleteTask, type Task, type ProjectMember, type TaskPriority } from '../../services/projectsApi';

const PRIORITIES: TaskPriority[] = ['Low', 'Medium', 'High'];

export function EditTaskModal({
  task,
  members,
  onClose,
  onChanged,
}: {
  task: Task;
  members: ProjectMember[];
  onClose: () => void;
  onChanged: () => Promise<void>;
}) {
  const [title, setTitle] = useState(task.title);
  const [description, setDescription] = useState(task.description ?? '');
  const [priority, setPriority] = useState<TaskPriority>(task.priority);
  const [assignedUserId, setAssignedUserId] = useState<string>(task.assignedUserId ?? '');
  const [dueDate, setDueDate] = useState(task.dueDate ? task.dueDate.slice(0, 10) : '');
  const [saving, setSaving] = useState(false);
  const [deleting, setDeleting] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!title.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await updateTask(task.id, {
        title: title.trim(),
        description: description.trim() || undefined,
        assignedUserId: assignedUserId || null,
        priority,
        dueDate: dueDate || null,
      });
      await onChanged();
    } catch (err: any) {
      setError(err?.message || 'Falha ao atualizar tarefa');
    } finally {
      setSaving(false);
    }
  };

  const handleDelete = async () => {
    setDeleting(true);
    setError(null);
    try {
      await deleteTask(task.id);
      await onChanged();
    } catch (err: any) {
      setError(err?.message || 'Falha ao excluir tarefa');
      setDeleting(false);
    }
  };

  return (
    <>
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm"
        onClick={onClose}
      />
      <motion.div
        initial={{ opacity: 0, scale: 0.95 }}
        animate={{ opacity: 1, scale: 1 }}
        className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[460px] max-w-[92vw] max-h-[90vh] bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl flex flex-col"
      >
        <div className="flex items-center justify-between p-5 pb-3 flex-shrink-0">
          <h3 className="text-[15px] font-semibold text-[#f5f7fb]">Editar Tarefa</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-[rgba(255,255,255,0.06)]">
            <X className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="px-5 overflow-y-auto min-h-0 pb-3 flex-1">
          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Título *</label>
          <input
            type="text"
            autoFocus
            value={title}
            onChange={(e) => setTitle(e.target.value)}
            maxLength={255}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Descrição</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={2000}
            rows={3}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)] resize-none"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Responsável</label>
          <select
            value={assignedUserId}
            onChange={(e) => setAssignedUserId(e.target.value)}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          >
            <option value="">— Ninguém —</option>
            {members.map((m) => (
              <option key={m.userId} value={m.userId}>
                {m.displayName}
              </option>
            ))}
          </select>

          <div className="grid grid-cols-2 gap-3 mb-3">
            <div>
              <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Prioridade</label>
              <div className="flex gap-1">
                {PRIORITIES.map((p) => (
                  <button
                    key={p}
                    type="button"
                    onClick={() => setPriority(p)}
                    className={`flex-1 py-2 rounded-lg text-[11px] font-semibold transition-colors ${
                      priority === p
                        ? p === 'High'
                          ? 'bg-[rgba(248,113,113,0.15)] text-[#f87171] border border-[rgba(248,113,113,0.3)]'
                          : p === 'Medium'
                            ? 'bg-[rgba(251,191,36,0.15)] text-[#fbbf24] border border-[rgba(251,191,36,0.3)]'
                            : 'bg-[rgba(96,165,250,0.15)] text-[#60a5fa] border border-[rgba(96,165,250,0.3)]'
                        : 'bg-[rgba(255,255,255,0.03)] text-[rgba(245,247,251,0.4)] border border-[rgba(255,255,255,0.06)] hover:bg-[rgba(255,255,255,0.06)]'
                    }`}
                  >
                    {p === 'High' ? 'Alta' : p === 'Medium' ? 'Média' : 'Baixa'}
                  </button>
                ))}
              </div>
            </div>
            <div>
              <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Prazo</label>
              <input
                type="date"
                value={dueDate}
                onChange={(e) => setDueDate(e.target.value)}
                className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
              />
            </div>
          </div>

          {error && <p className="text-[11px] text-[#f87171] mb-3">{error}</p>}
        </form>

        <div className="flex justify-between items-center p-5 pt-3 flex-shrink-0 border-t border-[rgba(255,255,255,0.04)]">
          {confirmDelete ? (
            <div className="flex items-center gap-2">
              <button
                onClick={handleDelete}
                disabled={deleting}
                className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-[11px] font-semibold bg-[rgba(248,113,113,0.15)] text-[#f87171] border border-[rgba(248,113,113,0.3)] hover:bg-[rgba(248,113,113,0.25)] transition-colors"
              >
                {deleting ? <Loader2 className="w-3 h-3 animate-spin" /> : <Trash2 className="w-3 h-3" />}
                Confirmar excluir
              </button>
              <button
                onClick={() => setConfirmDelete(false)}
                className="px-2 py-2 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)]"
              >
                Cancelar
              </button>
            </div>
          ) : (
            <button
              onClick={() => setConfirmDelete(true)}
              className="flex items-center gap-1.5 px-3 py-2 rounded-lg text-[11px] text-[rgba(248,113,113,0.6)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.08)] transition-colors"
            >
              <Trash2 className="w-3 h-3" />
              Excluir
            </button>
          )}

          <div className="flex items-center gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg text-[12px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)]"
            >
              Cancelar
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              disabled={saving || !title.trim()}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-[12px] font-semibold bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {saving && <Loader2 className="w-3 h-3 animate-spin" />}
              Salvar
            </button>
          </div>
        </div>
      </motion.div>
    </>
  );
}
