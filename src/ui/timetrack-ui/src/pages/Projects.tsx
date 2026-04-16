import { useEffect, useState, useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import { Plus, FolderOpen, Archive, Clock, DollarSign, LayoutGrid, AlertCircle } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useNavigate } from 'react-router-dom';
import { Sidebar } from '../components/dashboard';
import { useProjectStore, Project } from '../stores/projectStore';
import { ProjectCard, type ProjectStats } from '../components/projects/ProjectCard';
import { ProjectModal } from '../components/projects/ProjectModal';
import { SkeletonShimmer } from '../components/ui/SkeletonShimmer';
import { listMyTasks, type Task } from '../services/projectsApi';
import { usePermissions } from '../hooks/usePermissions';
import { fadeUp, staggerContainer, STAGGER, SPRING, TIMING } from '../lib/animation';

export default function Projects() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { canManageTeam } = usePermissions();
  const [showArchived, setShowArchived] = useState(false);
  const [mineOnly, setMineOnly] = useState(true);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingProject, setEditingProject] = useState<Project | null>(null);
  const [menuOpenId, setMenuOpenId] = useState<string | null>(null);
  const [allTasks, setAllTasks] = useState<Task[]>([]);

  const {
    projects,
    isLoading,
    error,
    fetchProjects,
    createProject,
    updateProject,
    archiveProject,
    reactivateProject,
    deleteProject,
  } = useProjectStore();

  useEffect(() => {
    fetchProjects(undefined, mineOnly);
    listMyTasks(true)
      .then((res) => setAllTasks(res.tasks))
      .catch(() => {});
  }, [fetchProjects, mineOnly]);

  // Per-project stats derived from all tasks
  const statsByProject = useMemo(() => {
    const map = new Map<string, ProjectStats>();
    allTasks.forEach((t) => {
      const s = map.get(t.projectId) ?? { todoCount: 0, inProgressCount: 0, doneCount: 0, totalSecondsWorked: 0 };
      if (t.status === 'Todo') s.todoCount++;
      else if (t.status === 'InProgress') s.inProgressCount++;
      else if (t.status === 'Done') s.doneCount++;
      s.totalSecondsWorked += t.totalSecondsWorked + (t.isRunning && t.runningSeconds ? t.runningSeconds : 0);
      map.set(t.projectId, s);
    });
    return map;
  }, [allTasks]);

  const activeProjects = projects.filter((p) => (p.status ?? '').toLowerCase() === 'active');
  const archivedProjects = projects.filter((p) => (p.status ?? '').toLowerCase() === 'archived');
  const displayedProjects = showArchived ? archivedProjects : activeProjects;

  // Global stats across active projects
  const totalSecondsWorked = useMemo(() => {
    return activeProjects.reduce((sum, p) => sum + (statsByProject.get(p.id)?.totalSecondsWorked ?? 0), 0);
  }, [activeProjects, statsByProject]);

  const totalBillableValue = useMemo(() => {
    return activeProjects.reduce((sum, p) => {
      if (!p.isBillable || !p.hourlyRate) return sum;
      const secs = statsByProject.get(p.id)?.totalSecondsWorked ?? 0;
      return sum + (secs / 3600) * p.hourlyRate;
    }, 0);
  }, [activeProjects, statsByProject]);

  const billableProjectsCount = activeProjects.filter((p) => p.isBillable).length;

  function fmtDuration(s: number): string {
    if (s <= 0) return '0h';
    const h = Math.floor(s / 3600);
    const m = Math.floor((s % 3600) / 60);
    if (h > 0 && m > 0) return `${h}h ${m}m`;
    if (h > 0) return `${h}h`;
    return `${m}m`;
  }

  const handleCreateProject = async (name: string, description: string, color: string, isBillable: boolean, currency: string | null, hourlyRate: number | null) => {
    const result = await createProject(name, description || undefined, color, isBillable, currency, hourlyRate);
    if (result) setIsModalOpen(false);
  };

  const handleUpdateProject = async (name: string, description: string, color: string, isBillable: boolean, currency: string | null, hourlyRate: number | null) => {
    if (!editingProject) return;
    const result = await updateProject(editingProject.id, name, description || undefined, color, isBillable, currency, hourlyRate);
    if (result) { setEditingProject(null); setIsModalOpen(false); }
  };

  const handleArchiveProject = async (id: string) => { await archiveProject(id); setMenuOpenId(null); };
  const handleReactivateProject = async (id: string) => { await reactivateProject(id); setMenuOpenId(null); };
  const handleDeleteProject = async (id: string) => {
    if (confirm(t('projects.deleteConfirm'))) { await deleteProject(id); setMenuOpenId(null); }
  };
  const openEditModal = (project: Project) => { setEditingProject(project); setIsModalOpen(true); setMenuOpenId(null); };
  const closeModal = () => { setIsModalOpen(false); setEditingProject(null); };

  return (
    <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0">
      <Sidebar />

      <main className="flex-1 overflow-auto bg-[#0b0d14]">
        <div className="max-w-6xl mx-auto px-5 py-6">

          {/* Header */}
          <motion.div
            className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 mb-6"
            variants={fadeUp}
            initial="hidden"
            animate="visible"
            transition={{ duration: TIMING.normal }}
          >
            <div>
              <h1 className="text-[24px] font-bold text-[#f5f7fb] tracking-tight">{t('projects.title')}</h1>
              <p className="text-[13px] text-[rgba(245,247,251,0.45)] mt-0.5">
                {t('projects.subtitle')}
              </p>
            </div>
            <motion.button
              onClick={() => { setEditingProject(null); setIsModalOpen(true); }}
              className="flex items-center gap-2 px-4 py-2.5 bg-gradient-to-r from-[#4A9FFF] to-[#3C7BFF] text-white rounded-xl font-semibold text-[13px] shadow-[0_4px_16px_rgba(74,159,255,0.3)] hover:shadow-[0_4px_20px_rgba(74,159,255,0.45)] transition-shadow flex-shrink-0"
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.97 }}
            >
              <Plus className="w-4 h-4" />
              {t('projects.newProject')}
            </motion.button>
          </motion.div>

          {/* Summary stats — only when there are active projects */}
          {!isLoading && activeProjects.length > 0 && (
            <motion.div
              className="grid grid-cols-3 gap-3 mb-6"
              variants={staggerContainer(STAGGER.cards)}
              initial="hidden"
              animate="visible"
            >
              <motion.div
                variants={fadeUp}
                className="px-4 py-3 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] flex items-center gap-3"
              >
                <div className="w-8 h-8 rounded-lg bg-[rgba(74,159,255,0.15)] flex items-center justify-center flex-shrink-0">
                  <LayoutGrid className="w-4 h-4 text-[#4A9FFF]" />
                </div>
                <div>
                  <p className="text-[18px] font-bold text-[#f5f7fb] leading-none">{activeProjects.length}</p>
                  <p className="text-[10px] text-[rgba(245,247,251,0.45)] mt-0.5">{t('projects.activeProjects')}</p>
                </div>
              </motion.div>

              <motion.div
                variants={fadeUp}
                className="px-4 py-3 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] flex items-center gap-3"
              >
                <div className="w-8 h-8 rounded-lg bg-[rgba(139,92,246,0.15)] flex items-center justify-center flex-shrink-0">
                  <Clock className="w-4 h-4 text-[#8B5CF6]" />
                </div>
                <div>
                  <p className="text-[18px] font-bold text-[#f5f7fb] leading-none">{fmtDuration(totalSecondsWorked)}</p>
                  <p className="text-[10px] text-[rgba(245,247,251,0.45)] mt-0.5">{t('projects.totalWorked')}</p>
                </div>
              </motion.div>

              <motion.div
                variants={fadeUp}
                className="px-4 py-3 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] flex items-center gap-3"
              >
                <div className="w-8 h-8 rounded-lg bg-[rgba(196,181,253,0.15)] flex items-center justify-center flex-shrink-0">
                  <DollarSign className="w-4 h-4 text-[#c4b5fd]" />
                </div>
                <div>
                  <p className="text-[18px] font-bold text-[#f5f7fb] leading-none">
                    {billableProjectsCount > 0 ? `${totalBillableValue.toFixed(2)}` : '—'}
                  </p>
                  <p className="text-[10px] text-[rgba(245,247,251,0.45)] mt-0.5">
                    {billableProjectsCount > 0 ? t('projects.billableValueWithCount', { count: billableProjectsCount }) : t('projects.noBillable')}
                  </p>
                </div>
              </motion.div>
            </motion.div>
          )}

          {/* Tabs */}
          <div className="flex flex-wrap items-center gap-2 mb-5">
            {canManageTeam && (
              <div className="flex items-center gap-1.5 px-1.5 py-1 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
                <button
                  onClick={() => setMineOnly(true)}
                  className={`px-3 py-1.5 rounded-lg text-[12px] font-medium transition-colors ${
                    mineOnly
                      ? 'bg-[rgba(139,92,246,0.14)] text-[#8B5CF6] border border-[rgba(139,92,246,0.25)]'
                      : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
                  }`}
                >
                  {t('reports.myData')}
                </button>
                <button
                  onClick={() => setMineOnly(false)}
                  className={`px-3 py-1.5 rounded-lg text-[12px] font-medium transition-colors ${
                    !mineOnly
                      ? 'bg-[rgba(139,92,246,0.14)] text-[#8B5CF6] border border-[rgba(139,92,246,0.25)]'
                      : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
                  }`}
                >
                  {t('reports.allTeam')}
                </button>
              </div>
            )}

            <div className="flex gap-2">
              {[
                { key: false, label: t('projects.active'), count: activeProjects.length, icon: FolderOpen },
                { key: true, label: t('projects.archived'), count: archivedProjects.length, icon: Archive },
              ].map(({ key, label, count, icon: Icon }) => (
                <button
                  key={String(key)}
                  onClick={() => setShowArchived(key)}
                  className={`relative flex items-center gap-2 px-4 py-2 rounded-xl text-[13px] font-medium transition-colors ${
                    showArchived === key
                      ? 'text-[#f5f7fb]'
                      : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.8)]'
                  }`}
                >
                  {showArchived === key && (
                    <motion.div
                      layoutId="projects-tab-bg"
                      className="absolute inset-0 bg-[rgba(255,255,255,0.07)] rounded-xl border border-[rgba(255,255,255,0.1)]"
                      transition={SPRING.snappy}
                    />
                  )}
                  <Icon className="w-3.5 h-3.5 relative z-10" />
                  <span className="relative z-10">{label}</span>
                  <span className={`relative z-10 px-1.5 py-0.5 rounded-md text-[10px] font-semibold ${
                    showArchived === key ? 'bg-[rgba(255,255,255,0.1)] text-[rgba(245,247,251,0.8)]' : 'bg-[rgba(255,255,255,0.05)] text-[rgba(245,247,251,0.4)]'
                  }`}>
                    {count}
                  </span>
                </button>
              ))}
            </div>
          </div>

          {/* Error */}
          {error && (
            <div className="mb-4 flex items-center gap-3 px-4 py-3 bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.2)] rounded-xl text-[12px] text-[#f87171]">
              <AlertCircle className="w-4 h-4 flex-shrink-0" />
              {error}
            </div>
          )}

          {/* Loading skeletons */}
          {isLoading && projects.length === 0 && (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="bg-[rgba(17,19,28,0.7)] border border-[rgba(255,255,255,0.06)] rounded-2xl overflow-hidden">
                  <div className="h-[3px] w-full bg-[rgba(255,255,255,0.06)]" />
                  <div className="p-4 space-y-3">
                    <SkeletonShimmer width="55%" height={14} rounded="rounded-lg" />
                    <SkeletonShimmer width="85%" height={10} rounded="rounded-lg" />
                    <SkeletonShimmer width="65%" height={10} rounded="rounded-lg" />
                    <div className="flex gap-2 pt-1">
                      <SkeletonShimmer width={72} height={24} rounded="rounded-lg" />
                      <SkeletonShimmer width={80} height={24} rounded="rounded-lg" />
                    </div>
                  </div>
                </div>
              ))}
            </div>
          )}

          {/* Empty state */}
          <AnimatePresence mode="wait">
            {!isLoading && displayedProjects.length === 0 && (
              <motion.div
                className="text-center py-16"
                variants={fadeUp}
                initial="hidden"
                animate="visible"
                exit="exit"
                transition={{ duration: TIMING.normal }}
              >
                <div className="w-16 h-16 rounded-2xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] flex items-center justify-center mx-auto mb-4">
                  <FolderOpen className="w-7 h-7 text-[rgba(245,247,251,0.2)]" />
                </div>
                <p className="text-[14px] font-medium text-[rgba(245,247,251,0.4)]">
                  {showArchived ? t('projects.noArchived') : t('projects.noProjects')}
                </p>
                {!showArchived && (
                  <button
                    onClick={() => setIsModalOpen(true)}
                    className="mt-3 text-[13px] text-[#4A9FFF] hover:underline"
                  >
                    {t('projects.createFirst')}
                  </button>
                )}
              </motion.div>
            )}
          </AnimatePresence>

          {/* Projects grid */}
          <AnimatePresence mode="wait">
            <motion.div
              key={showArchived ? 'archived' : 'active'}
              className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
              variants={staggerContainer(STAGGER.cards)}
              initial="hidden"
              animate="visible"
              exit="exit"
            >
              {displayedProjects.map((project) => (
                <motion.div
                  key={project.id}
                  variants={fadeUp}
                  layout
                  transition={{ duration: TIMING.normal }}
                  onClick={(e) => {
                    if ((e.target as HTMLElement).closest('button')) return;
                    navigate(`/projects/${project.id}/board`);
                  }}
                  className="cursor-pointer"
                >
                  <ProjectCard
                    project={project}
                    stats={statsByProject.get(project.id)}
                    isMenuOpen={menuOpenId === project.id}
                    onToggleMenu={() => setMenuOpenId(menuOpenId === project.id ? null : project.id)}
                    onEdit={() => openEditModal(project)}
                    onArchive={() => handleArchiveProject(project.id)}
                    onReactivate={() => handleReactivateProject(project.id)}
                    onDelete={() => handleDeleteProject(project.id)}
                    isArchived={project.status === 'Archived'}
                  />
                </motion.div>
              ))}
            </motion.div>
          </AnimatePresence>

        </div>
      </main>

      <AnimatePresence>
        {isModalOpen && (
          <ProjectModal
            project={editingProject}
            onClose={closeModal}
            onSubmit={editingProject ? handleUpdateProject : handleCreateProject}
            isLoading={isLoading}
          />
        )}
      </AnimatePresence>
    </div>
  );
}
