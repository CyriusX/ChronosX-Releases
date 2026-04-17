/**
 * ProjectBoard — kanban board for a single project (desktop agent).
 * The user can only drag cards assigned to themselves; other cards are read-only.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useNavigate, Navigate } from 'react-router-dom';
import { motion, AnimatePresence } from 'motion/react';
import { ArrowLeft, Users, Loader2, RefreshCw, Zap, ExternalLink, Clock, ListTodo, DollarSign } from 'lucide-react';
import { Sidebar } from '../components/dashboard';
import { KanbanBoard } from '../components/projects/KanbanBoard';
import { ProjectMembersModal } from '../components/projects/ProjectMembersModal';
import {
  getProject,
  listProjectTasks,
  listProjectMembers,
  type Task,
  type ProjectItem,
  type ProjectMember,
} from '../services/projectsApi';
import { syncLinear } from '../services/integrationsApi';
import { useNotifications } from '../stores/uiStore';
import { usePermissions } from '../hooks/usePermissions';

const POLL_INTERVAL_MS = 15_000;

export default function ProjectBoard() {
  const { t } = useTranslation();
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const { notify } = useNotifications();
  const { canManageTeam } = usePermissions();

  const [project, setProject] = useState<ProjectItem | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [syncingLinear, setSyncingLinear] = useState(false);
  const [membersOpen, setMembersOpen] = useState(false);

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const isLinearProject = project?.syncSource === 'Linear';

  if (!projectId) {
    return <Navigate to="/projects" replace />;
  }

  const fetchAll = useCallback(async (showSpinner: boolean) => {
    if (showSpinner) setLoading(true);
    else setRefreshing(true);
    try {
      const [p, tasksRes, membersRes] = await Promise.all([
        getProject(projectId).catch(() => null),
        listProjectTasks(projectId),
        listProjectMembers(projectId).catch(() => ({ members: [], totalCount: 0 })),
      ]);
      if (p) setProject(p);
      setTasks(tasksRes.tasks ?? []);
      setMembers(membersRes.members ?? []);
    } catch (err) {
      console.error('[ProjectBoard] fetch failed', err);
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [projectId]);

  const handleLinearSync = async () => {
    setSyncingLinear(true);
    try {
      const res = await syncLinear();
      const total = res.tasksCreated + res.tasksUpdated;
      notify.success(
        total > 0
          ? t('projects.linearSynced', { created: res.tasksCreated, updated: res.tasksUpdated })
          : t('projects.linearUpToDate'),
      );
      await fetchAll(false);
    } catch (err) {
      const msg = err && typeof err === 'object' && 'message' in err && typeof (err as { message?: unknown }).message === 'string'
        ? (err as { message: string }).message
        : t('projects.linearSyncFailed');
      notify.error(msg);
    } finally {
      setSyncingLinear(false);
    }
  };

  useEffect(() => {
    fetchAll(true);
    pollRef.current = setInterval(() => fetchAll(false), POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; }
    };
  }, [fetchAll]);

  const handleOptimisticChange = (next: Task[]) => setTasks(next);
  const handleConflict = () => fetchAll(false);

  const handleTaskCreated = (task: Task) => {
    setTasks((prev) => [...prev, task]);
  };

  const handleTaskDeleted = (taskId: string) => {
    setTasks((prev) => prev.filter((t) => t.id !== taskId));
  };

  // Include running seconds in billable cost so it reflects live work in progress.
  const totalSecondsWorked = tasks.reduce((sum, t) => {
    const running = t.isRunning && t.runningSeconds ? t.runningSeconds : 0;
    return sum + t.totalSecondsWorked + running;
  }, 0);
  const todoCount = tasks.filter((t) => t.status === 'Todo').length;
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

  return (
    <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0">
      <Sidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0 overflow-hidden">
        {/* Header */}
        <div className="px-4 lg:px-6 pt-4 pb-2 flex-shrink-0">
          <button
            onClick={() => navigate('/projects')}
            className="flex items-center gap-1.5 text-[11px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] transition-colors mb-2"
          >
            <ArrowLeft className="w-3.5 h-3.5" />
            {t('projects.title')}
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
                <h1 className="text-[18px] sm:text-[22px] font-semibold text-[#f5f7fb] truncate">
                  {project?.name ?? t('projects.project')}
                </h1>
                {isLinearProject && (
                  <span className="flex items-center gap-1 px-2 py-0.5 rounded-full bg-[rgba(94,106,210,0.12)] border border-[rgba(94,106,210,0.3)] text-[9px] font-semibold text-[#a78bfa]">
                    <Zap className="w-2.5 h-2.5" />
                    LINEAR
                  </span>
                )}
              </div>
              {project?.description && (
                <p className="text-[12px] text-[rgba(245,247,251,0.5)] max-w-[600px]">
                  {project.description}
                </p>
              )}
              {isLinearProject && project?.lastSyncedAt && (
                <p className="text-[10px] text-[rgba(245,247,251,0.35)] mt-1">
                  {t('projects.syncedFromLinear')} {formatRelative(project.lastSyncedAt)}
                </p>
              )}
            </div>

            <div className="flex items-center gap-2">
              {!isLinearProject && (
                <div className="flex items-center gap-2 h-9 px-3 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)]">
                  <Users className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
                  <span className="text-[11px] text-[rgba(245,247,251,0.6)]">
                    {members.length} {members.length !== 1 ? t('projects.membersCount') : t('projects.member')}
                  </span>
                </div>
              )}

              {!isLinearProject && canManageTeam && (
                <motion.button
                  onClick={() => setMembersOpen(true)}
                  whileHover={{ scale: 1.05 }}
                  whileTap={{ scale: 0.95 }}
                  className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                  title={t('projects.manageMembersTooltip')}
                >
                  <Users className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
                </motion.button>
              )}

              {isLinearProject && (
                <motion.button
                  onClick={handleLinearSync}
                  disabled={syncingLinear}
                  whileHover={{ scale: syncingLinear ? 1 : 1.04 }}
                  whileTap={{ scale: 0.97 }}
                  className="flex items-center gap-2 h-9 px-4 rounded-[10px] bg-gradient-to-r from-[#5e6ad2] to-[#a78bfa] text-white text-[11px] font-semibold shadow-[0_4px_12px_rgba(94,106,210,0.3)] disabled:opacity-60 transition-colors"
                  title={t('projects.pullLinearUpdates')}
                >
                  <RefreshCw className={`w-3.5 h-3.5 ${syncingLinear ? 'animate-spin' : ''}`} />
                  {syncingLinear ? t('projects.syncing') : t('projects.syncLinear')}
                </motion.button>
              )}

              {isLinearProject && project?.linearProjectId && (
                <a
                  href={`https://linear.app/team/project/${encodeURIComponent(project.linearProjectId)}`}
                  target="_blank"
                  rel="noreferrer"
                  className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                  title={t('projects.openInLinear')}
                >
                  <ExternalLink className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
                </a>
              )}

              <motion.button
                onClick={() => fetchAll(false)}
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                title={t('common.refresh')}
              >
                <RefreshCw className={`w-3.5 h-3.5 text-[rgba(245,247,251,0.6)] ${refreshing ? 'animate-spin' : ''}`} />
              </motion.button>
            </div>
          </header>
        </div>

        {/* Project stats bar */}
        {!loading && (
          <div className="px-4 lg:px-6 pb-3 flex-shrink-0">
            <div className="flex flex-wrap items-center gap-4 px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
              <div className="flex items-center gap-1.5">
                <Clock className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.7)]">{formatTotalWorked(totalSecondsWorked)} {t('projects.worked')}</span>
              </div>
              <div className="flex items-center gap-1.5">
                <ListTodo className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.7)]">{todoCount} {t('projects.todoCount')}</span>
              </div>
              {totalCost !== null && (
                <>
                  <div className="h-3 w-px bg-[rgba(255,255,255,0.1)]" />
                  <div className="flex items-center gap-1.5">
                    <DollarSign className="w-3.5 h-3.5 text-[#c4b5fd]" />
                    <span className="text-[11px] font-semibold text-[#c4b5fd]">{formatCost(totalCost, project?.currency || 'USD')}</span>
                    <span className="text-[10px] text-[rgba(245,247,251,0.4)]">{t('projects.billableLabel')}</span>
                  </div>
                </>
              )}
            </div>
          </div>
        )}

        {/* Info banner — only your cards are draggable */}
        <div className="px-4 lg:px-6 pb-3 flex-shrink-0">
          <div className="px-3 py-2 rounded-lg bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)]">
            <p className="text-[11px] text-[rgba(245,247,251,0.7)]">
              <span className="font-semibold text-[#c4b5fd]">{t('projects.tip')}</span> {t('projects.tipText')}
            </p>
          </div>
        </div>

        {/* Kanban board */}
        <div className="flex-1 overflow-hidden min-h-0 px-4 lg:px-6 pb-4">
          {loading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="w-5 h-5 text-[#8B5CF6] animate-spin" />
            </div>
          ) : (
            <KanbanBoard
              tasks={tasks}
              projectId={projectId}
              projectColor={project?.color ?? '#8B5CF6'}
              syncSource={project?.syncSource ?? 'Local'}
              onLocalChange={handleOptimisticChange}
              onConflict={handleConflict}
              onTaskCreated={handleTaskCreated}
              onTaskDeleted={handleTaskDeleted}
            />
          )}
        </div>
      </main>

      <AnimatePresence>
        {membersOpen && project && (
          <ProjectMembersModal
            projectId={project.id}
            projectName={project.name}
            onClose={() => setMembersOpen(false)}
            onChanged={() => fetchAll(false)}
          />
        )}
      </AnimatePresence>
    </div>
  );
}

function formatRelative(iso: string): string {
  const diff = Math.floor((Date.now() - new Date(iso).getTime()) / 1000);
  if (diff < 60) return 'agora';
  if (diff < 3600) return `há ${Math.floor(diff / 60)}min`;
  if (diff < 86400) return `há ${Math.floor(diff / 3600)}h`;
  return `há ${Math.floor(diff / 86400)}d`;
}
