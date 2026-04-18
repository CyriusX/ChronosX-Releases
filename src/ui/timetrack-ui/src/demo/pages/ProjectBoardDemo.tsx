/**
 * Demo-only ProjectBoard page.
 *
 * - embed mode: matches the real ProjectBoard layout (horizontal columns)
 * - mobile mode: vertical-only Kanban using a segmented status switcher (no horizontal scroll)
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useParams, useNavigate, Navigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { ArrowLeft, RefreshCw, ExternalLink } from 'lucide-react';
import { KanbanBoard } from '../../components/projects/KanbanBoard';
import { TaskCard } from '../../components/projects/TaskCard';
import { TaskDetailDrawer } from '../../components/projects/TaskDetailDrawer';
import {
  getProject,
  listProjectTasks,
  type Task,
  type ProjectItem,
} from '../../services/projectsApi';
import { syncLinear } from '../../services/integrationsApi';
import { useNotifications } from '../../stores/uiStore';
import { getDemoMode } from '../demoMode';

const POLL_INTERVAL_MS = 15_000;

type TaskStatus = 'Todo' | 'InProgress' | 'InReview' | 'Done';

function getColumns(syncSource: ProjectItem['syncSource'] | undefined): TaskStatus[] {
  return syncSource === 'Linear'
    ? ['Todo', 'InProgress', 'InReview', 'Done']
    : ['Todo', 'InProgress', 'Done'];
}

export default function ProjectBoardDemo() {
  const { t } = useTranslation();
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const { notify } = useNotifications();
  const demoMode = getDemoMode();
  const isMobileDemo = demoMode === 'mobile';

  const [project, setProject] = useState<ProjectItem | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [syncingLinear, setSyncingLinear] = useState(false);
  const [drawerTaskId, setDrawerTaskId] = useState<string | null>(null);
  const [mobileStatus, setMobileStatus] = useState<TaskStatus>('Todo');

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const isLinearProject = project?.syncSource === 'Linear';
  const columns = useMemo(() => getColumns(project?.syncSource), [project?.syncSource]);

  useEffect(() => {
    if (!columns.includes(mobileStatus)) {
      setMobileStatus(columns[0] ?? 'Todo');
    }
  }, [columns, mobileStatus]);

  const fetchAll = useCallback(async (initial = false) => {
    if (!projectId) return;
    if (initial) setLoading(true);
    else setRefreshing(true);

    try {
      const [p, tasksRes] = await Promise.all([
        getProject(projectId),
        listProjectTasks(projectId),
      ]);
      setProject(p ?? null);
      setTasks(tasksRes.tasks ?? []);
    } catch (err) {
      console.error('[ProjectBoardDemo] fetch failed', err);
      notify.error(t('common.error'));
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [notify, projectId, t]);

  useEffect(() => {
    if (!projectId) return;
    fetchAll(true);
  }, [fetchAll, projectId]);

  useEffect(() => {
    if (!projectId) return;
    if (pollRef.current) clearInterval(pollRef.current);
    pollRef.current = setInterval(() => fetchAll(false), POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) clearInterval(pollRef.current);
      pollRef.current = null;
    };
  }, [fetchAll, projectId]);

  const handleLinearSync = async () => {
    setSyncingLinear(true);
    try {
      const res = await syncLinear();
      const total = (res.tasksCreated ?? 0) + (res.tasksUpdated ?? 0);
      notify.success(
        total > 0
          ? t('projects.linearSynced', { created: res.tasksCreated, updated: res.tasksUpdated })
          : t('projects.linearUpToDate'),
      );
      await fetchAll(false);
    } catch {
      notify.error(t('projects.linearSyncFailed'));
    } finally {
      setSyncingLinear(false);
    }
  };

  if (!projectId) return <Navigate to="/projects" replace />;

  if (!project && !loading) {
    return (
      <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0 overflow-x-hidden">
        <main className="flex-1 flex items-center justify-center">
          <div className="text-[12px] text-[rgba(245,247,251,0.5)]">{t('projects.noActiveProjects')}</div>
        </main>
      </div>
    );
  }

  const header = (
    <div className="flex flex-col gap-3">
      <div className="flex items-start justify-between gap-3 min-w-0">
        <div className="flex items-start gap-3 min-w-0">
          <button
            onClick={() => navigate(-1)}
            className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors flex-shrink-0"
            aria-label={t('common.back')}
          >
            <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
          </button>
          <div className="min-w-0">
            <div className="flex items-center gap-2 min-w-0 flex-wrap">
              <h1 className="text-[16px] font-semibold text-[#f5f7fb] truncate max-w-[60vw]">
                {project?.name ?? '—'}
              </h1>
              {isLinearProject && (
                <span className="px-2 py-0.5 rounded-full text-[10px] font-semibold bg-[rgba(94,106,210,0.15)] text-[#aab3ff] border border-[rgba(94,106,210,0.25)]">
                  Linear
                </span>
              )}
            </div>
            {project?.description && (
              <p className="text-[11px] text-[rgba(245,247,251,0.35)] mt-0.5 line-clamp-2">
                {project.description}
              </p>
            )}
          </div>
        </div>

        <div className="flex items-center gap-2 flex-shrink-0 flex-wrap justify-end">
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
        </div>
      </div>

      {/* Mobile-only segmented control (vertical-only board) */}
      {isMobileDemo && (
        <div className="grid grid-cols-2 gap-2" data-demo-kanban-segments>
          {columns.map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setMobileStatus(s)}
              className={`rounded-xl border px-3 py-2 text-[12px] font-semibold transition-colors ${
                mobileStatus === s
                  ? 'border-[rgba(139,92,246,0.35)] bg-[rgba(139,92,246,0.16)] text-[#f5f7fb]'
                  : 'border-[rgba(255,255,255,0.06)] bg-[rgba(255,255,255,0.03)] text-[rgba(245,247,251,0.6)]'
              }`}
            >
              {s === 'Todo' ? t('kanban.todo')
                : s === 'InProgress' ? t('kanban.inProgress')
                : s === 'InReview' ? t('kanban.inReview')
                : t('kanban.done')}
            </button>
          ))}
        </div>
      )}
    </div>
  );

  if (!isMobileDemo) {
    // Embed mode: keep the real board layout (including horizontal columns).
    return (
      <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0">
        <main className="flex-1 flex flex-col min-w-0 min-h-0">
          <div className="px-5 pt-4 pb-2 flex-shrink-0">{header}</div>
          <div className="flex-1 px-5 pb-4 min-h-0 overflow-hidden">
            {loading ? (
              <div className="h-full rounded-2xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] animate-pulse" />
            ) : (
              <KanbanBoard
                tasks={tasks}
                projectId={projectId}
                projectColor={project?.color ?? '#8B5CF6'}
                syncSource={project?.syncSource ?? 'Local'}
                onConflict={() => fetchAll(false)}
                onTaskDeleted={() => fetchAll(false)}
                onTaskUpdated={() => fetchAll(false)}
              />
            )}
          </div>
        </main>
      </div>
    );
  }

  // Mobile mode: vertical-only Kanban (no horizontal scroll).
  const tasksByStatus = useMemo(() => {
    const by: Record<TaskStatus, Task[]> = { Todo: [], InProgress: [], InReview: [], Done: [] };
    for (const task of tasks) {
      const status = (task.status as TaskStatus) ?? 'Todo';
      (by[status] ?? by.Todo).push(task);
    }
    return by;
  }, [tasks]);

  const visibleTasks = tasksByStatus[mobileStatus] ?? [];

  return (
    <div className="flex h-screen bg-[#0b0d14] overflow-hidden pb-14 md:pb-0 overflow-x-hidden">
      <main
        className="flex-1 flex flex-col min-w-0 min-h-0 px-5 pt-4 pb-4 overflow-y-auto overflow-x-hidden"
        data-demo-scroll-root="kanban"
      >
        {header}

        <div className="mt-4 flex flex-col gap-2">
          {loading ? (
            Array.from({ length: 6 }).map((_, i) => (
              <div
                key={i}
                className="h-16 rounded-2xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] animate-pulse"
              />
            ))
          ) : visibleTasks.length === 0 ? (
            <div className="text-center py-10 text-[12px] text-[rgba(245,247,251,0.35)] italic">
              {t('kanban.noTasks')}
            </div>
          ) : (
            visibleTasks.map((task) => (
              <TaskCard
                key={task.id}
                task={task}
                projectColor={project?.color ?? '#8B5CF6'}
                draggable={false}
                onOpen={() => setDrawerTaskId(task.id)}
              />
            ))
          )}
          <div data-demo-marker="kanban-bottom" className="h-px w-full" />
        </div>

        <TaskDetailDrawer
          taskId={drawerTaskId}
          onClose={() => setDrawerTaskId(null)}
          onDeleted={() => setDrawerTaskId(null)}
          onAssigned={() => {}}
        />
      </main>
    </div>
  );
}
