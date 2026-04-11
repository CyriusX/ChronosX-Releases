/**
 * ProjectDetail — Kanban board for a single project.
 * Polls every 15s so manager sees user-driven changes without manual refresh.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useParams, useNavigate, Navigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { ArrowLeft, Users, Plus, Loader2, RefreshCw, Zap, ExternalLink } from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { KanbanBoard } from '../components/projects/KanbanBoard';
import { AddMemberModal } from '../components/projects/AddMemberModal';
import { CreateTaskModal } from '../components/projects/CreateTaskModal';
import { EditTaskModal } from '../components/projects/EditTaskModal';
import { usePermissions } from '../hooks/usePermissions';
import {
  getProject,
  listProjectTasks,
  listProjectMembers,
  type ProjectItem,
  type Task,
  type TaskStatus,
  type ProjectMember,
} from '../services/projectsApi';
import { syncLinear } from '../services/integrationsApi';

const POLL_INTERVAL_MS = 15_000;

export default function ProjectDetail() {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const { canManageTeam } = usePermissions();

  const [project, setProject] = useState<ProjectItem | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const [showAddMember, setShowAddMember] = useState(false);
  const [showCreateTask, setShowCreateTask] = useState(false);
  const [createTaskStatus, setCreateTaskStatus] = useState<TaskStatus>('Todo');
  const [editingTask, setEditingTask] = useState<Task | null>(null);
  const [syncingLinear, setSyncingLinear] = useState(false);
  const [syncError, setSyncError] = useState<string | null>(null);

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const isLinearProject = project?.syncSource === 'Linear';

  if (!canManageTeam) {
    return <Navigate to="/" replace />;
  }
  if (!projectId) {
    return <Navigate to="/projects" replace />;
  }

  const fetchAll = useCallback(async (showSpinner: boolean) => {
    if (showSpinner) setLoading(true);
    else setRefreshing(true);
    try {
      const [p, t, m] = await Promise.all([
        getProject(projectId).catch(() => null),
        listProjectTasks(projectId),
        listProjectMembers(projectId).catch(() => ({ members: [], totalCount: 0 })),
      ]);
      if (p) setProject(p);
      setTasks(t.tasks ?? []);
      setMembers(m.members ?? []);
    } catch (err) {
      console.error('[ProjectDetail] fetch failed', err);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [projectId]);

  useEffect(() => {
    fetchAll(true);
    pollRef.current = setInterval(() => fetchAll(false), POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; }
    };
  }, [fetchAll]);

  const handleTaskCreateClick = (status: TaskStatus) => {
    setCreateTaskStatus(status);
    setShowCreateTask(true);
  };

  const handleOptimisticChange = (next: Task[]) => {
    setTasks(next);
  };

  const handleConflict = () => {
    // Refetch to get authoritative state
    fetchAll(false);
  };

  const totalSecondsWorked = tasks.reduce((sum, t) => sum + t.totalSecondsWorked, 0);
  const totalCost = project?.isBillable && project?.hourlyRate
    ? (totalSecondsWorked / 3600) * project.hourlyRate
    : null;

  const formatCost = (cost: number, currency: string): string => {
    return `${currency} ${cost.toFixed(2)}`;
  };

  const formatTotalWorked = (seconds: number): string => {
    if (seconds < 60) return '0m';
    const h = Math.floor(seconds / 3600);
    const m = Math.floor((seconds % 3600) / 60);
    if (h === 0) return `${m}m`;
    return `${h}h ${m}m`;
  };

  const handleLinearSync = async () => {
    setSyncingLinear(true);
    setSyncError(null);
    try {
      await syncLinear();
      await fetchAll(false);
    } catch (err: unknown) {
      const msg =
        (err && typeof err === 'object' && 'message' in err && typeof (err as { message?: unknown }).message === 'string'
          ? (err as { message: string }).message
          : 'Falha ao sincronizar com o Linear.');
      setSyncError(msg);
    } finally {
      setSyncingLinear(false);
    }
  };

  return (
    <div className="flex h-screen bg-transparent overflow-hidden pt-[52px] md:pt-0">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header */}
        <div className="px-4 lg:px-5 pt-4 pb-2 flex-shrink-0">
          <button
            onClick={() => navigate('/projects')}
            className="flex items-center gap-1.5 text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] transition-colors mb-2"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            Projetos
          </button>
          <header className="flex items-start justify-between gap-3 flex-wrap">
            <div className="min-w-0 flex-1">
              <div className="flex items-center gap-2 mb-1">
                {project && (
                  <span
                    className="w-3 h-3 rounded-full flex-shrink-0"
                    style={{ backgroundColor: project.color }}
                  />
                )}
                <h1 className="text-[16px] sm:text-[20px] font-semibold text-[#f5f7fb] truncate">
                  {project?.name ?? 'Projeto'}
                </h1>
                {isLinearProject && (
                  <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-[rgba(94,106,210,0.12)] border border-[rgba(94,106,210,0.3)] text-[9px] font-semibold text-[#a78bfa]">
                    <Zap className="w-2.5 h-2.5" />
                    LINEAR
                  </span>
                )}
              </div>
              {project?.description && (
                <p className="text-[11px] sm:text-[12px] text-[rgba(245,247,251,0.4)] max-w-[600px]">
                  {project.description}
                </p>
              )}
              {isLinearProject && project?.lastSyncedAt && (
                <p className="text-[10px] text-[rgba(245,247,251,0.35)] mt-1">
                  Sincronizado do Linear · última sincronização {formatRelative(project.lastSyncedAt)}
                </p>
              )}
              {syncError && (
                <p className="text-[11px] text-[#f87171] mt-1">{syncError}</p>
              )}
            </div>

            <div className="flex items-center gap-2">
              {/* Members chips — hidden on Linear projects since membership is not managed there */}
              {!isLinearProject && (
                <button
                  onClick={() => setShowAddMember(true)}
                  className="flex items-center gap-2 h-9 px-3 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                  title="Gerenciar membros"
                >
                  <Users className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
                  <div className="flex -space-x-1.5">
                    {members.slice(0, 4).map((m) => (
                      <div
                        key={m.id}
                        className="w-5 h-5 rounded-full flex items-center justify-center text-[8px] font-bold text-white border border-[#0b0d14]"
                        style={{ backgroundColor: project?.color ?? '#8B5CF6' }}
                        title={m.displayName}
                      >
                        {initials(m.displayName)}
                      </div>
                    ))}
                    {members.length > 4 && (
                      <div className="w-5 h-5 rounded-full flex items-center justify-center text-[8px] font-bold text-[rgba(245,247,251,0.6)] bg-[rgba(255,255,255,0.08)] border border-[#0b0d14]">
                        +{members.length - 4}
                      </div>
                    )}
                  </div>
                  <span className="text-[11px] text-[rgba(245,247,251,0.6)] hidden sm:inline">
                    {members.length === 0 ? 'Adicionar' : `${members.length} membro${members.length !== 1 ? 's' : ''}`}
                  </span>
                </button>
              )}

              {/* New Task button is hidden on Linear projects (issues come from Linear) */}
              {!isLinearProject && (
                <motion.button
                  onClick={() => handleTaskCreateClick('Todo')}
                  whileHover={{ scale: 1.04 }}
                  whileTap={{ scale: 0.97 }}
                  className="flex items-center gap-2 h-9 px-4 rounded-[10px] bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white text-[11px] font-semibold shadow-[0_4px_12px_rgba(139,92,246,0.3)] hover:from-[#7c3aed] hover:to-[#6d28d9] transition-colors"
                >
                  <Plus className="w-3.5 h-3.5" />
                  Nova Tarefa
                </motion.button>
              )}

              {/* Sync from Linear — only on Linear-synced projects */}
              {isLinearProject && (
                <motion.button
                  onClick={handleLinearSync}
                  disabled={syncingLinear}
                  whileHover={{ scale: syncingLinear ? 1 : 1.04 }}
                  whileTap={{ scale: 0.97 }}
                  className="flex items-center gap-2 h-9 px-4 rounded-[10px] bg-gradient-to-r from-[#5e6ad2] to-[#a78bfa] text-white text-[11px] font-semibold shadow-[0_4px_12px_rgba(94,106,210,0.3)] disabled:opacity-60 transition-colors"
                  title="Puxar atualizações do Linear"
                >
                  <RefreshCw className={`w-3.5 h-3.5 ${syncingLinear ? 'animate-spin' : ''}`} />
                  {syncingLinear ? 'Sincronizando…' : 'Sincronizar Linear'}
                </motion.button>
              )}

              {/* Open in Linear link */}
              {isLinearProject && project?.linearProjectId && (
                <a
                  href={`https://linear.app/team/project/${encodeURIComponent(project.linearProjectId)}`}
                  target="_blank"
                  rel="noreferrer"
                  className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                  title="Abrir no Linear"
                >
                  <ExternalLink className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
                </a>
              )}

              {/* Refresh */}
              <button
                onClick={() => fetchAll(false)}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                title="Atualizar"
              >
                <RefreshCw className={`w-3.5 h-3.5 text-[rgba(245,247,251,0.6)] ${refreshing ? 'animate-spin' : ''}`} />
              </button>
            </div>
          </header>
        </div>

        {/* Cost Display — for billable projects */}
        {totalCost !== null && (
          <div className="px-4 lg:px-5 pb-3 flex-shrink-0">
            <div className="px-3 py-2 rounded-lg bg-gradient-to-r from-[rgba(139,92,246,0.12)] to-[rgba(139,92,246,0.06)] border border-[rgba(139,92,246,0.2)]">
              <p className="text-[11px] text-[rgba(245,247,251,0.8)]">
                <span className="font-semibold text-[#c4b5fd]">{formatCost(totalCost, project?.currency || 'USD')}</span>
                <span className="mx-2">·</span>
                <span>{formatTotalWorked(totalSecondsWorked)} trabalhado</span>
              </p>
            </div>
          </div>
        )}

        {/* Kanban board */}
        <div className="flex-1 overflow-hidden min-h-0 px-4 lg:px-5 pb-4 pt-2">
          {loading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="w-5 h-5 text-[#8B5CF6] animate-spin" />
            </div>
          ) : (
            <KanbanBoard
              tasks={tasks}
              projectColor={project?.color ?? '#8B5CF6'}
              syncSource={project?.syncSource ?? 'Local'}
              onTaskCreateClick={handleTaskCreateClick}
              onTaskEdit={setEditingTask}
              onLocalChange={handleOptimisticChange}
              onConflict={handleConflict}
            />
          )}
        </div>
      </main>

      {showAddMember && project && (
        <AddMemberModal
          projectId={project.id}
          existingMembers={members}
          onClose={() => setShowAddMember(false)}
          onChanged={async () => {
            await fetchAll(false);
          }}
        />
      )}

      {showCreateTask && project && (
        <CreateTaskModal
          projectId={project.id}
          initialStatus={createTaskStatus}
          members={members}
          onClose={() => setShowCreateTask(false)}
          onCreated={async () => {
            setShowCreateTask(false);
            await fetchAll(false);
          }}
        />
      )}

      {editingTask && project && (
        <EditTaskModal
          task={editingTask}
          members={members}
          onClose={() => setEditingTask(null)}
          onChanged={async () => {
            setEditingTask(null);
            await fetchAll(false);
          }}
        />
      )}
    </div>
  );
}

function initials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0][0]?.toUpperCase() ?? '?';
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function formatRelative(iso: string): string {
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (diff < 60) return 'agora';
  if (diff < 3600) return `há ${Math.floor(diff / 60)}min`;
  if (diff < 86400) return `há ${Math.floor(diff / 3600)}h`;
  return `há ${Math.floor(diff / 86400)}d`;
}
