/**
 * MemberDetailDrawer — right-side panel with full member data and manager actions.
 * Opens when a MemberCard is clicked.
 */

import { useEffect, useState, useCallback } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import {
  X, Clock, Zap, Target, Folder, Trash2, Plus, ChevronDown,
  CheckSquare, Circle, PlayCircle, ArrowRight, AlertTriangle,
} from 'lucide-react';
import { getMemberSummary } from '../../services/memberApi';
import {
  listUserTasks,
  deleteTask,
  createTask,
  removeProjectMember,
  listProjects,
  type Task,
  type TaskPriority,
  type ProjectItem,
} from '../../services/projectsApi';
import type { TeamMemberStatus, MemberSummaryResponse } from '../../types/member';
import { getMemberGradient } from '../dashboard/shared/styles';

interface MemberDetailDrawerProps {
  member: TeamMemberStatus;
  onClose: () => void;
}

type TaskTab = 'todo' | 'inprogress' | 'done';

function formatDuration(seconds: number): string {
  if (!seconds || seconds <= 0) return '0m';
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  if (h > 0) return `${h}h ${String(m).padStart(2, '0')}m`;
  return `${m}m`;
}

function getInitials(name: string): string {
  return name.split(' ').filter(Boolean).slice(0, 2).map(w => w[0].toUpperCase()).join('');
}

function roleLabel(role: string): string {
  switch (role) {
    case 'Admin': return 'Admin';
    case 'Gestor': return 'Gestor';
    default: return 'Membro';
  }
}

function priorityColor(p: TaskPriority): string {
  switch (p) {
    case 'Urgent': return '#ef4444';
    case 'High': return '#fb923c';
    case 'Medium': return '#fbbf24';
    case 'Low': return '#60a5fa';
    default: return '#94a3b8';
  }
}

function priorityLabel(p: TaskPriority): string {
  switch (p) {
    case 'Urgent': return 'Urgente';
    case 'High': return 'Alta';
    case 'Medium': return 'Média';
    case 'Low': return 'Baixa';
    default: return 'Nenhuma';
  }
}

function formatDate(iso: string | null | undefined): string {
  if (!iso) return '';
  const d = new Date(iso.split('T')[0] + 'T00:00:00');
  return d.toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' });
}

interface DerivedProject {
  id: string;
  name: string;
  color: string;
}

export function MemberDetailDrawer({ member, onClose }: MemberDetailDrawerProps) {
  const [summary, setSummary] = useState<MemberSummaryResponse | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);
  const [activeTab, setActiveTab] = useState<TaskTab>('inprogress');
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);
  const [removingProject, setRemovingProject] = useState<string | null>(null);

  // Add task form
  const [showAddTask, setShowAddTask] = useState(false);
  const [availableProjects, setAvailableProjects] = useState<ProjectItem[]>([]);
  const [newTaskProjectId, setNewTaskProjectId] = useState('');
  const [newTaskTitle, setNewTaskTitle] = useState('');
  const [newTaskPriority, setNewTaskPriority] = useState<TaskPriority>('Medium');
  const [newTaskDueDate, setNewTaskDueDate] = useState('');
  const [addingTask, setAddingTask] = useState(false);
  const [showProjectPicker, setShowProjectPicker] = useState(false);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [sumRes, tasksRes] = await Promise.all([
        getMemberSummary(member.userId),
        listUserTasks(member.userId, true),
      ]);
      setSummary(sumRes);
      setTasks(tasksRes.tasks ?? []);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }, [member.userId]);

  useEffect(() => {
    loadData();
    // Close on Escape
    const handleKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [loadData, onClose]);

  // Load available projects for add-task form
  useEffect(() => {
    if (showAddTask && availableProjects.length === 0) {
      listProjects(true).then(r => {
        setAvailableProjects(r.projects ?? []);
        if (r.projects?.length > 0) setNewTaskProjectId(r.projects[0].id);
      }).catch(() => {});
    }
  }, [showAddTask, availableProjects.length]);

  // Derive projects from tasks
  const projects: DerivedProject[] = (() => {
    const map = new Map<string, DerivedProject>();
    for (const t of tasks) {
      if (t.projectId && !map.has(t.projectId)) {
        map.set(t.projectId, { id: t.projectId, name: t.projectName, color: t.projectColor });
      }
    }
    return [...map.values()];
  })();

  const todoTasks = tasks.filter(t => t.status === 'Todo');
  const inProgressTasks = tasks.filter(t => t.status === 'InProgress' || t.status === 'InReview');
  const doneTasks = tasks.filter(t => t.status === 'Done');

  const displayedTasks = activeTab === 'todo' ? todoTasks : activeTab === 'inprogress' ? inProgressTasks : doneTasks;

  async function handleDeleteTask(taskId: string) {
    if (confirmDeleteId !== taskId) {
      setConfirmDeleteId(taskId);
      return;
    }
    setDeletingId(taskId);
    setConfirmDeleteId(null);
    try {
      await deleteTask(taskId);
      setTasks(prev => prev.filter(t => t.id !== taskId));
    } catch {
      // ignore
    } finally {
      setDeletingId(null);
    }
  }

  async function handleRemoveFromProject(projectId: string) {
    setRemovingProject(projectId);
    try {
      await removeProjectMember(projectId, member.userId);
      setTasks(prev => prev.filter(t => t.projectId !== projectId));
    } catch {
      // ignore
    } finally {
      setRemovingProject(null);
    }
  }

  async function handleAddTask() {
    if (!newTaskTitle.trim() || !newTaskProjectId) return;
    setAddingTask(true);
    try {
      const task = await createTask(newTaskProjectId, {
        title: newTaskTitle.trim(),
        priority: newTaskPriority,
        dueDate: newTaskDueDate || null,
      });
      // Assign to member via update
      setTasks(prev => [...prev, { ...task, assignedUserId: member.userId, assignedUserDisplayName: member.displayName }]);
      setNewTaskTitle('');
      setNewTaskDueDate('');
      setShowAddTask(false);
      setActiveTab('todo');
    } catch {
      // ignore
    } finally {
      setAddingTask(false);
    }
  }

  const gradient = getMemberGradient(member.displayName);
  const prodPct = summary ? (summary.totalDuration > 0 ? Math.round((summary.productiveTime / summary.totalDuration) * 100) : 0) : null;

  return (
    <>
      {/* Backdrop */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 bg-[rgba(0,0,0,0.5)] backdrop-blur-sm z-40"
        onClick={onClose}
      />

      {/* Panel */}
      <motion.aside
        initial={{ x: '100%' }}
        animate={{ x: 0 }}
        exit={{ x: '100%' }}
        transition={{ type: 'spring', stiffness: 260, damping: 30 }}
        className="fixed right-0 top-0 bottom-0 w-[480px] max-w-full bg-[rgb(14,16,26)] border-l border-[rgba(255,255,255,0.07)] z-50 flex flex-col overflow-hidden"
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center gap-3 px-5 py-4 border-b border-[rgba(255,255,255,0.06)] flex-shrink-0">
          <div className={`w-12 h-12 rounded-full bg-gradient-to-br ${gradient} flex items-center justify-center shadow-[0_4px_12px_rgba(0,0,0,0.4)]`}>
            <span className="text-white text-[14px] font-bold">{getInitials(member.displayName)}</span>
          </div>
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-2">
              <h2 className="text-[15px] font-semibold text-[#f5f7fb] truncate">{member.displayName}</h2>
              <div className="relative flex-shrink-0">
                {member.isTracking && (
                  <motion.div
                    className="absolute inset-0 rounded-full bg-[#05df72]"
                    animate={{ opacity: [0.4, 0.1, 0.4], scale: [1, 1.8, 1] }}
                    transition={{ duration: 2, repeat: Infinity }}
                  />
                )}
                <div className={`w-2 h-2 rounded-full relative ${member.isTracking ? 'bg-[#05df72]' : 'bg-[rgba(245,247,251,0.2)]'}`} />
              </div>
            </div>
            <span className="text-[11px] text-[rgba(245,247,251,0.45)]">{roleLabel(member.role)}</span>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Scrollable content */}
        <div className="flex-1 overflow-y-auto">
          {loading ? (
            <div className="flex items-center justify-center py-20">
              <div className="w-6 h-6 border-2 border-[rgba(139,92,246,0.4)] border-t-[#8B5CF6] rounded-full animate-spin" />
            </div>
          ) : (
            <div className="p-5 space-y-5">
              {/* Summary cards */}
              <div className="grid grid-cols-3 gap-3">
                <div className="rounded-[14px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] p-3 text-center">
                  <Clock className="w-4 h-4 text-[#22D3EE] mx-auto mb-1" />
                  <div className="text-[14px] font-semibold text-[#f5f7fb]">{formatDuration(summary?.totalDuration ?? 0)}</div>
                  <div className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase tracking-wide mt-0.5">Hoje</div>
                </div>
                <div className="rounded-[14px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] p-3 text-center">
                  <Target className="w-4 h-4 text-[#10B981] mx-auto mb-1" />
                  <div className="text-[14px] font-semibold" style={{ color: prodPct !== null ? (prodPct >= 70 ? '#05df72' : prodPct >= 40 ? '#fbbf24' : '#f87171') : '#94a3b8' }}>
                    {prodPct !== null ? `${prodPct}%` : '—'}
                  </div>
                  <div className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase tracking-wide mt-0.5">Produt.</div>
                </div>
                <div className="rounded-[14px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] p-3 text-center">
                  <Zap className="w-4 h-4 text-[#F59E0B] mx-auto mb-1" />
                  <div className="text-[14px] font-semibold text-[#fbbf24]">{summary?.focusScore ?? '—'}</div>
                  <div className="text-[9px] text-[rgba(245,247,251,0.4)] uppercase tracking-wide mt-0.5">Foco</div>
                </div>
              </div>

              {/* Projects */}
              {projects.length > 0 && (
                <div>
                  <h3 className="text-[11px] font-medium text-[rgba(245,247,251,0.45)] uppercase tracking-wider mb-2.5 flex items-center gap-1.5">
                    <Folder className="w-3.5 h-3.5" />
                    Projetos
                  </h3>
                  <div className="space-y-2">
                    {projects.map(p => (
                      <div key={p.id} className="flex items-center gap-3 px-3 py-2.5 rounded-[12px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] group">
                        <div className="w-2.5 h-2.5 rounded-full flex-shrink-0" style={{ backgroundColor: p.color || '#8B5CF6' }} />
                        <span className="text-[12px] text-[rgba(245,247,251,0.8)] flex-1 truncate">{p.name}</span>
                        <button
                          onClick={() => handleRemoveFromProject(p.id)}
                          disabled={removingProject === p.id}
                          className="text-[10px] text-[rgba(248,113,113,0.6)] hover:text-[#f87171] transition-colors opacity-0 group-hover:opacity-100 flex items-center gap-1 disabled:opacity-40"
                        >
                          {removingProject === p.id ? (
                            <div className="w-3 h-3 border border-[#f87171] border-t-transparent rounded-full animate-spin" />
                          ) : (
                            <>
                              <X className="w-3 h-3" />
                              Remover
                            </>
                          )}
                        </button>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Tasks */}
              <div>
                <div className="flex items-center justify-between mb-3">
                  <h3 className="text-[11px] font-medium text-[rgba(245,247,251,0.45)] uppercase tracking-wider flex items-center gap-1.5">
                    <CheckSquare className="w-3.5 h-3.5" />
                    Tarefas
                  </h3>
                  <button
                    onClick={() => setShowAddTask(v => !v)}
                    className="flex items-center gap-1 text-[10px] text-[rgba(139,92,246,0.8)] hover:text-[#8B5CF6] transition-colors"
                  >
                    <Plus className="w-3 h-3" />
                    Adicionar
                  </button>
                </div>

                {/* Add task form */}
                <AnimatePresence>
                  {showAddTask && (
                    <motion.div
                      initial={{ height: 0, opacity: 0 }}
                      animate={{ height: 'auto', opacity: 1 }}
                      exit={{ height: 0, opacity: 0 }}
                      transition={{ duration: 0.2 }}
                      className="overflow-hidden mb-3"
                    >
                      <div className="rounded-[14px] border border-[rgba(139,92,246,0.25)] bg-[rgba(139,92,246,0.06)] p-3 space-y-2.5">
                        {/* Project picker */}
                        <div className="relative">
                          <button
                            type="button"
                            onClick={() => setShowProjectPicker(v => !v)}
                            className="w-full flex items-center gap-2 px-2.5 py-1.5 rounded-lg bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)] text-[11px] text-[rgba(245,247,251,0.7)] hover:border-[rgba(255,255,255,0.15)] transition-colors"
                          >
                            <Folder className="w-3 h-3 flex-shrink-0" />
                            <span className="flex-1 text-left truncate">
                              {availableProjects.find(p => p.id === newTaskProjectId)?.name ?? 'Selecionar projeto'}
                            </span>
                            <ChevronDown className="w-3 h-3 flex-shrink-0" />
                          </button>
                          <AnimatePresence>
                            {showProjectPicker && (
                              <motion.div
                                initial={{ opacity: 0, y: -4 }}
                                animate={{ opacity: 1, y: 0 }}
                                exit={{ opacity: 0, y: -4 }}
                                className="absolute top-full left-0 right-0 mt-1 rounded-[12px] bg-[rgb(20,22,35)] border border-[rgba(255,255,255,0.1)] shadow-xl z-10 max-h-40 overflow-y-auto"
                              >
                                {availableProjects.map(p => (
                                  <button
                                    key={p.id}
                                    type="button"
                                    onClick={() => { setNewTaskProjectId(p.id); setShowProjectPicker(false); }}
                                    className="w-full flex items-center gap-2 px-3 py-2 text-[11px] text-[rgba(245,247,251,0.7)] hover:bg-[rgba(255,255,255,0.05)] transition-colors text-left"
                                  >
                                    <div className="w-2 h-2 rounded-full flex-shrink-0" style={{ backgroundColor: p.color || '#8B5CF6' }} />
                                    {p.name}
                                  </button>
                                ))}
                              </motion.div>
                            )}
                          </AnimatePresence>
                        </div>

                        {/* Title */}
                        <input
                          type="text"
                          placeholder="Título da tarefa"
                          value={newTaskTitle}
                          onChange={e => setNewTaskTitle(e.target.value)}
                          onKeyDown={e => { if (e.key === 'Enter') handleAddTask(); if (e.key === 'Escape') setShowAddTask(false); }}
                          className="w-full px-2.5 py-1.5 rounded-lg bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)] text-[12px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] transition-colors"
                          autoFocus
                        />

                        {/* Priority + Due date row */}
                        <div className="flex gap-2">
                          <select
                            value={newTaskPriority}
                            onChange={e => setNewTaskPriority(e.target.value as TaskPriority)}
                            className="flex-1 px-2.5 py-1.5 rounded-lg bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)] text-[11px] text-[rgba(245,247,251,0.7)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] transition-colors"
                          >
                            <option value="None">Prioridade: Nenhuma</option>
                            <option value="Low">Baixa</option>
                            <option value="Medium">Média</option>
                            <option value="High">Alta</option>
                            <option value="Urgent">Urgente</option>
                          </select>
                          <input
                            type="date"
                            value={newTaskDueDate}
                            onChange={e => setNewTaskDueDate(e.target.value)}
                            className="flex-1 px-2.5 py-1.5 rounded-lg bg-[rgba(255,255,255,0.05)] border border-[rgba(255,255,255,0.08)] text-[11px] text-[rgba(245,247,251,0.7)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] transition-colors"
                          />
                        </div>

                        {/* Actions */}
                        <div className="flex gap-2">
                          <button
                            type="button"
                            onClick={() => setShowAddTask(false)}
                            className="flex-1 px-3 py-1.5 rounded-lg text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
                          >
                            Cancelar
                          </button>
                          <button
                            type="button"
                            onClick={handleAddTask}
                            disabled={addingTask || !newTaskTitle.trim() || !newTaskProjectId}
                            className="flex-1 px-3 py-1.5 rounded-lg text-[11px] font-medium bg-[rgba(139,92,246,0.2)] text-[#8B5CF6] border border-[rgba(139,92,246,0.3)] hover:bg-[rgba(139,92,246,0.3)] disabled:opacity-40 disabled:cursor-not-allowed transition-colors"
                          >
                            {addingTask ? 'Criando...' : 'Criar Tarefa'}
                          </button>
                        </div>
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>

                {/* Tabs */}
                <div className="flex gap-1 mb-3 p-0.5 rounded-[10px] bg-[rgba(255,255,255,0.04)]">
                  {([
                    { id: 'inprogress' as TaskTab, label: 'Em andamento', count: inProgressTasks.length, icon: PlayCircle },
                    { id: 'todo' as TaskTab, label: 'A fazer', count: todoTasks.length, icon: Circle },
                    { id: 'done' as TaskTab, label: 'Feitas', count: doneTasks.length, icon: CheckSquare },
                  ] as const).map(tab => (
                    <button
                      key={tab.id}
                      onClick={() => setActiveTab(tab.id)}
                      className={`flex-1 flex items-center justify-center gap-1.5 py-1.5 rounded-[8px] text-[10px] font-medium transition-all ${
                        activeTab === tab.id
                          ? 'bg-[rgba(139,92,246,0.15)] text-[#8B5CF6] shadow-sm'
                          : 'text-[rgba(245,247,251,0.45)] hover:text-[rgba(245,247,251,0.7)]'
                      }`}
                    >
                      <tab.icon className="w-3 h-3" />
                      <span>{tab.label}</span>
                      {tab.count > 0 && (
                        <span className={`px-1.5 py-0.5 rounded-full text-[9px] ${activeTab === tab.id ? 'bg-[rgba(139,92,246,0.25)] text-[#8B5CF6]' : 'bg-[rgba(255,255,255,0.06)] text-[rgba(245,247,251,0.4)]'}`}>
                          {tab.count}
                        </span>
                      )}
                    </button>
                  ))}
                </div>

                {/* Task list */}
                <div className="space-y-1.5">
                  {displayedTasks.length === 0 ? (
                    <div className="text-center py-8 text-[11px] text-[rgba(245,247,251,0.3)] italic">
                      Nenhuma tarefa nesta categoria
                    </div>
                  ) : (
                    displayedTasks.map(task => (
                      <div key={task.id} className="flex items-start gap-2.5 px-3 py-2.5 rounded-[12px] bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)] hover:border-[rgba(255,255,255,0.1)] transition-colors group">
                        {/* Priority dot */}
                        <div className="w-1.5 h-1.5 rounded-full mt-1.5 flex-shrink-0" style={{ backgroundColor: priorityColor(task.priority) }} />

                        <div className="flex-1 min-w-0">
                          <p className="text-[12px] text-[rgba(245,247,251,0.85)] leading-tight">{task.title}</p>
                          <div className="flex items-center gap-2 mt-1 flex-wrap">
                            <span className="text-[10px] text-[rgba(245,247,251,0.35)]">{task.projectName}</span>
                            {task.priority !== 'None' && (
                              <span className="text-[9px] font-medium" style={{ color: priorityColor(task.priority) }}>
                                {priorityLabel(task.priority)}
                              </span>
                            )}
                            {task.dueDate && (
                              <span className="flex items-center gap-0.5 text-[9px] text-[rgba(245,247,251,0.3)]">
                                <ArrowRight className="w-2.5 h-2.5" />
                                {formatDate(task.dueDate)}
                              </span>
                            )}
                          </div>
                        </div>

                        {/* Delete */}
                        <div className="flex items-center gap-1 flex-shrink-0 opacity-0 group-hover:opacity-100 transition-opacity">
                          {confirmDeleteId === task.id ? (
                            <>
                              <span className="text-[9px] text-[#f87171]">Confirmar?</span>
                              <button
                                onClick={() => handleDeleteTask(task.id)}
                                disabled={deletingId === task.id}
                                className="p-1 rounded text-[#f87171] hover:bg-[rgba(248,113,113,0.1)] transition-colors"
                              >
                                {deletingId === task.id ? (
                                  <div className="w-3 h-3 border border-[#f87171] border-t-transparent rounded-full animate-spin" />
                                ) : (
                                  <AlertTriangle className="w-3 h-3" />
                                )}
                              </button>
                              <button
                                onClick={() => setConfirmDeleteId(null)}
                                className="p-1 rounded text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
                              >
                                <X className="w-3 h-3" />
                              </button>
                            </>
                          ) : (
                            <button
                              onClick={() => handleDeleteTask(task.id)}
                              className="p-1 rounded text-[rgba(245,247,251,0.25)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.08)] transition-colors"
                            >
                              <Trash2 className="w-3.5 h-3.5" />
                            </button>
                          )}
                        </div>
                      </div>
                    ))
                  )}
                </div>
              </div>
            </div>
          )}
        </div>
      </motion.aside>
    </>
  );
}
