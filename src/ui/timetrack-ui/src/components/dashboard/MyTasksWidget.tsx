/**
 * MyTasksWidget — shows the user's current in-progress task (with live timer)
 * plus a short list of Todo cards assigned to them. Lets the user start or
 * complete tasks with one click.
 *
 * Polls the backend every 30s. The running timer ticks locally each second so
 * the user sees a smooth count without hammering the network.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { PlayCircle, CheckCircle2, Pause, ListTodo, Loader2 } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { Card, CardContent, CardHeader, CardTitle } from '../ui/card';
import { cardBase as sharedCardBase } from './shared/styles';
import { useIpc } from '../../hooks/useIpc';
import {
  listMyTasks,
  moveTask,
  type Task,
} from '../../services/projectsApi';
import { onProjectUpdated, onTaskDeleted, onTaskUpdated } from '../../lib/appEvents';

const POLL_INTERVAL_MS = 30_000;
const cardBase = sharedCardBase + ' overflow-hidden';

function formatHms(seconds: number): string {
  const h = Math.floor(seconds / 3600);
  const m = Math.floor((seconds % 3600) / 60);
  const s = seconds % 60;
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
  return `${m}:${String(s).padStart(2, '0')}`;
}

export function MyTasksWidget() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { subscribeToEvent } = useIpc();
  const [tasks, setTasks] = useState<Task[]>([]);
  const [loading, setLoading] = useState(true);
  const [busyTaskId, setBusyTaskId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [tick, setTick] = useState(0); // forces re-render for live timer
  const lastFetchedAtRef = useRef<number>(Date.now()); // tracks when tasks were last fetched

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const fetchTasks = useCallback(async () => {
    try {
      const res = await listMyTasks(false);
      setTasks(res.tasks ?? []);
      lastFetchedAtRef.current = Date.now(); // record fetch time so live timer resets
      setError(null);
    } catch (err: any) {
      setError(err?.message || t('dashboard.failedToLoadTasks'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTasks();
    pollRef.current = setInterval(fetchTasks, POLL_INTERVAL_MS);
    return () => {
      if (pollRef.current) { clearInterval(pollRef.current); pollRef.current = null; }
    };
  }, [fetchTasks]);

  // Refetch immediately when the agent pushes a change (manager assigned/updated/unassigned a task,
  // or the task idle watcher auto-paused the timer)
  useEffect(() => {
    const unsubTasks = subscribeToEvent('myTasksChanged', () => {
      fetchTasks();
    });
    const unsubIdle = subscribeToEvent('taskIdleAutoPaused', () => {
      fetchTasks();
    });
    return () => {
      unsubTasks();
      unsubIdle();
    };
  }, [subscribeToEvent, fetchTasks]);

  // Immediate UI feedback for local edits (task/project title changes) without waiting for polling.
  useEffect(() => {
    const offTaskUpdated = onTaskUpdated((updated) => {
      setTasks((prev) => prev.map((t) => (t.id === updated.id ? { ...t, ...updated } : t)));
    });
    const offTaskDeleted = onTaskDeleted((taskId) => {
      setTasks((prev) => prev.filter((t) => t.id !== taskId));
    });
    const offProjectUpdated = onProjectUpdated((p: any) => {
      if (!p?.id) return;
      setTasks((prev) =>
        prev.map((t) =>
          t.projectId === p.id
            ? {
                ...t,
                projectName: typeof p.name === 'string' ? p.name : t.projectName,
                projectColor: typeof p.color === 'string' ? p.color : t.projectColor,
              }
            : t,
        ),
      );
    });
    return () => {
      offTaskUpdated();
      offTaskDeleted();
      offProjectUpdated();
    };
  }, []);

  // Tick every second for live running timer
  useEffect(() => {
    const hasRunning = tasks.some((t) => t.isRunning);
    if (!hasRunning) return;
    const id = setInterval(() => setTick((v) => v + 1), 1000);
    return () => clearInterval(id);
  }, [tasks]);

  const inProgress = tasks.find((t) => t.status === 'InProgress');
  const todos = tasks.filter((t) => t.status === 'Todo').slice(0, 5);

  const handleStart = async (task: Task) => {
    setBusyTaskId(task.id);
    try {
      await moveTask(task.id, { status: 'InProgress', rowVersion: task.rowVersion });
      await fetchTasks();
    } catch (err: any) {
      setError(err?.message || t('dashboard.failedToStartTask'));
    } finally {
      setBusyTaskId(null);
    }
  };

  const handleStop = async (task: Task) => {
    setBusyTaskId(task.id);
    try {
      await moveTask(task.id, { status: 'Todo', rowVersion: task.rowVersion });
      await fetchTasks();
    } catch (err: any) {
      setError(err?.message || t('dashboard.failedToStopTask'));
    } finally {
      setBusyTaskId(null);
    }
  };

  const handleComplete = async (task: Task) => {
    setBusyTaskId(task.id);
    try {
      await moveTask(task.id, { status: 'Done', rowVersion: task.rowVersion });
      await fetchTasks();
    } catch (err: any) {
      setError(err?.message || t('dashboard.failedToCompleteTask'));
    } finally {
      setBusyTaskId(null);
    }
  };

  // Compute live seconds for the in-progress task: server-reported value at last fetch
  // plus elapsed seconds since that fetch. tick triggers re-render each second.
  const liveSecondsFor = (task: Task): number => {
    void tick;
    const base = (task.runningSeconds ?? 0) + task.totalSecondsWorked;
    if (!task.isRunning) return base;
    const elapsedSinceLastFetch = Math.floor((Date.now() - lastFetchedAtRef.current) / 1000);
    return base + elapsedSinceLastFetch;
  };

  if (loading && tasks.length === 0) {
    return (
      <Card className={cardBase}>
        <CardHeader className="pb-0 pt-3 px-4">
          <CardTitle>
            <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{t('dashboard.myTasks')}</span>
          </CardTitle>
        </CardHeader>
        <CardContent className="pt-2 pb-3 px-4 flex items-center justify-center">
          <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
        </CardContent>
      </Card>
    );
  }

  if (tasks.length === 0) {
    return null; // hide widget when user has no tasks assigned
  }

  return (
    <Card className={cardBase}>
      <CardHeader className="pb-0 pt-3 px-4">
        <CardTitle className="flex items-center justify-between">
          <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">{t('dashboard.myTasks')}</span>
          <span className="text-[10px] text-[rgba(245,247,251,0.4)]">{tasks.length}</span>
        </CardTitle>
      </CardHeader>
      <CardContent className="pt-2 pb-3 px-4 space-y-3">
        {/* In Progress — show as hero card with live timer */}
        {inProgress && (
          <motion.div
            layout
            className="relative rounded-lg overflow-hidden p-3 border"
            style={{
              backgroundColor: `${inProgress.projectColor}10`,
              borderColor: `${inProgress.projectColor}40`,
            }}
          >
            {/* Live indicator */}
            <div className="flex items-center gap-1.5 mb-1.5">
              <PlayCircle className="w-3 h-3 animate-pulse" style={{ color: inProgress.projectColor }} />
              <span className="text-[9px] font-bold uppercase tracking-wider" style={{ color: inProgress.projectColor }}>
                {t('dashboard.running')}
              </span>
            </div>

            <button
              onClick={() => navigate(`/projects/${inProgress.projectId}/board`)}
              className="text-left w-full"
            >
              <p className="text-[12px] font-semibold text-[#f5f7fb] leading-snug mb-0.5 hover:underline">
                {inProgress.title}
              </p>
              <p className="text-[9px] text-[rgba(245,247,251,0.5)] truncate">{inProgress.projectName}</p>
            </button>

            <div className="mt-2 flex items-center justify-between">
              <span className="text-[16px] font-bold font-mono tabular-nums text-[#f5f7fb]">
                {formatHms(liveSecondsFor(inProgress))}
              </span>
              <div className="flex items-center gap-1">
                <button
                  onClick={() => handleStop(inProgress)}
                  disabled={busyTaskId === inProgress.id}
                  className="flex items-center gap-1 px-2 py-1 rounded-md text-[10px] font-semibold bg-[rgba(251,191,36,0.12)] text-[#fbbf24] hover:bg-[rgba(251,191,36,0.2)] disabled:opacity-40 transition-colors"
                  title={t('dashboard.stop')}
                >
                  {busyTaskId === inProgress.id ? <Loader2 className="w-3 h-3 animate-spin" /> : <Pause className="w-3 h-3" />}
                  {t('dashboard.stop')}
                </button>
                <button
                  onClick={() => handleComplete(inProgress)}
                  disabled={busyTaskId === inProgress.id}
                  className="flex items-center gap-1 px-2 py-1 rounded-md text-[10px] font-semibold bg-[rgba(5,223,114,0.12)] text-[#05df72] hover:bg-[rgba(5,223,114,0.2)] disabled:opacity-40 transition-colors"
                  title={t('dashboard.complete')}
                >
                  {busyTaskId === inProgress.id ? <Loader2 className="w-3 h-3 animate-spin" /> : <CheckCircle2 className="w-3 h-3" />}
                  {t('dashboard.complete')}
                </button>
              </div>
            </div>
          </motion.div>
        )}

        {/* Todo list */}
        {todos.length > 0 && (
          <div>
            <div className="flex items-center gap-1.5 mb-2">
              <ListTodo className="w-3 h-3 text-[rgba(245,247,251,0.4)]" />
              <span className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)]">{t('dashboard.todo')}</span>
            </div>
            <div className="space-y-1.5">
              {todos.map((task) => (
                <div
                  key={task.id}
                  className="group flex items-center gap-2 p-2 rounded-md bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.04)] hover:border-[rgba(255,255,255,0.08)] transition-colors"
                >
                  <span
                    className="w-1.5 h-1.5 rounded-full flex-shrink-0"
                    style={{ backgroundColor: task.projectColor }}
                  />
                  <button
                    onClick={() => navigate(`/projects/${task.projectId}/board`)}
                    className="flex-1 min-w-0 text-left"
                  >
                    <p className="text-[11px] font-medium text-[#f5f7fb] truncate hover:underline">{task.title}</p>
                    <p className="text-[9px] text-[rgba(245,247,251,0.4)] truncate">{task.projectName}</p>
                  </button>
                  <button
                    onClick={() => handleStart(task)}
                    disabled={busyTaskId === task.id || !!inProgress}
                    title={inProgress ? t('dashboard.finishCurrentFirst') : t('dashboard.start')}
                    className="flex items-center gap-1 px-2 py-1 rounded-md text-[9px] font-semibold bg-[rgba(139,92,246,0.12)] text-[#c4b5fd] hover:bg-[rgba(139,92,246,0.2)] disabled:opacity-30 disabled:cursor-not-allowed transition-colors"
                  >
                    {busyTaskId === task.id ? (
                      <Loader2 className="w-3 h-3 animate-spin" />
                    ) : (
                      <PlayCircle className="w-3 h-3" />
                    )}
                    {t('dashboard.start')}
                  </button>
                </div>
              ))}
            </div>
          </div>
        )}

        {error && <p className="text-[10px] text-[#f87171]">{error}</p>}
      </CardContent>
    </Card>
  );
}
