/**
 * ProjectBoard — kanban board for a single project (desktop agent).
 * The user can only drag cards assigned to themselves; other cards are read-only.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { useParams, useNavigate, Navigate } from 'react-router-dom';
import { motion } from 'motion/react';
import { ArrowLeft, Users, Loader2, RefreshCw } from 'lucide-react';
import { Sidebar } from '../components/dashboard';
import { KanbanBoard } from '../components/projects/KanbanBoard';
import { useAuthStore } from '../stores/authStore';
import {
  listProjectTasks,
  listProjectMembers,
  listProjects,
  type Task,
  type ProjectItem,
  type ProjectMember,
} from '../services/projectsApi';

const POLL_INTERVAL_MS = 15_000;

export default function ProjectBoard() {
  const { projectId } = useParams<{ projectId: string }>();
  const navigate = useNavigate();
  const user = useAuthStore((s) => s.user);

  const [project, setProject] = useState<ProjectItem | null>(null);
  const [tasks, setTasks] = useState<Task[]>([]);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const pollRef = useRef<ReturnType<typeof setInterval> | null>(null);

  if (!projectId) {
    return <Navigate to="/projects" replace />;
  }

  const fetchAll = useCallback(async (showSpinner: boolean) => {
    if (showSpinner) setLoading(true);
    else setRefreshing(true);
    try {
      // No single /projects/{id} endpoint in timetrack-ui's apiClient helpers;
      // fetch the list and pick the one we want.
      const [projectsRes, tasksRes, membersRes] = await Promise.all([
        listProjects(true).catch(() => ({ projects: [], totalCount: 0 })),
        listProjectTasks(projectId),
        listProjectMembers(projectId).catch(() => ({ members: [], totalCount: 0 })),
      ]);
      const found = projectsRes.projects.find((p) => p.id === projectId) ?? null;
      setProject(found);
      setTasks(tasksRes.tasks ?? []);
      setMembers(membersRes.members ?? []);
    } catch (err) {
      console.error('[ProjectBoard] fetch failed', err);
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

  const handleOptimisticChange = (next: Task[]) => setTasks(next);
  const handleConflict = () => fetchAll(false);

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
                <h1 className="text-[18px] sm:text-[22px] font-semibold text-[#f5f7fb] truncate">
                  {project?.name ?? 'Projeto'}
                </h1>
              </div>
              {project?.description && (
                <p className="text-[12px] text-[rgba(245,247,251,0.5)] max-w-[600px]">
                  {project.description}
                </p>
              )}
            </div>

            <div className="flex items-center gap-2">
              <div className="flex items-center gap-2 h-9 px-3 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)]">
                <Users className="w-3.5 h-3.5 text-[rgba(245,247,251,0.6)]" />
                <span className="text-[11px] text-[rgba(245,247,251,0.6)]">
                  {members.length} membro{members.length !== 1 ? 's' : ''}
                </span>
              </div>

              <motion.button
                onClick={() => fetchAll(false)}
                whileHover={{ scale: 1.05 }}
                whileTap={{ scale: 0.95 }}
                className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
                title="Atualizar"
              >
                <RefreshCw className={`w-3.5 h-3.5 text-[rgba(245,247,251,0.6)] ${refreshing ? 'animate-spin' : ''}`} />
              </motion.button>
            </div>
          </header>
        </div>

        {/* Info banner — only your cards are draggable */}
        <div className="px-4 lg:px-6 pb-3 flex-shrink-0">
          <div className="px-3 py-2 rounded-lg bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)]">
            <p className="text-[11px] text-[rgba(245,247,251,0.7)]">
              <span className="font-semibold text-[#c4b5fd]">Dica:</span> Arraste suas tarefas para "Em Progresso" para iniciar o timer automaticamente. Mover para "Concluído" para encerrar.
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
              projectColor={project?.color ?? '#8B5CF6'}
              currentUserId={user?.id ?? null}
              onLocalChange={handleOptimisticChange}
              onConflict={handleConflict}
            />
          )}
        </div>
      </main>
    </div>
  );
}
