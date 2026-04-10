import { useEffect, useState } from 'react';
import { Plus, FolderOpen, Archive } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { Sidebar } from '../components/dashboard';
import { useProjectStore, Project } from '../stores/projectStore';
import { ProjectCard } from '../components/projects/ProjectCard';
import { ProjectModal } from '../components/projects/ProjectModal';
import { SkeletonShimmer } from '../components/ui/SkeletonShimmer';
import { fadeUp, staggerContainer, STAGGER, SPRING, TIMING } from '../lib/animation';

export default function Projects() {
  const [showArchived, setShowArchived] = useState(false);
  const [isModalOpen, setIsModalOpen] = useState(false);
  const [editingProject, setEditingProject] = useState<Project | null>(null);
  const [menuOpenId, setMenuOpenId] = useState<string | null>(null);

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
    fetchProjects();
  }, [fetchProjects]);

  const activeProjects = projects.filter(p => p.status === 'Active');
  const archivedProjects = projects.filter(p => p.status === 'Archived');
  const displayedProjects = showArchived ? archivedProjects : activeProjects;

  const handleCreateProject = async (name: string, description: string, color: string) => {
    const result = await createProject(name, description || undefined, color);
    if (result) {
      setIsModalOpen(false);
    }
  };

  const handleUpdateProject = async (name: string, description: string, color: string) => {
    if (!editingProject) return;
    const result = await updateProject(editingProject.id, name, description || undefined, color);
    if (result) {
      setEditingProject(null);
      setIsModalOpen(false);
    }
  };

  const handleArchiveProject = async (id: string) => {
    await archiveProject(id);
    setMenuOpenId(null);
  };

  const handleReactivateProject = async (id: string) => {
    await reactivateProject(id);
    setMenuOpenId(null);
  };

  const handleDeleteProject = async (id: string) => {
    if (confirm('Tem certeza que deseja excluir este projeto?')) {
      await deleteProject(id);
      setMenuOpenId(null);
    }
  };

  const openEditModal = (project: Project) => {
    setEditingProject(project);
    setIsModalOpen(true);
    setMenuOpenId(null);
  };

  const closeModal = () => {
    setIsModalOpen(false);
    setEditingProject(null);
  };

  return (
    <div className="flex h-screen bg-[#0b0d14] pb-14 md:pb-0">
      <Sidebar />

      <main className="flex-1 overflow-auto bg-[#0b0d14] p-6">
        <div className="max-w-6xl mx-auto">
          {/* Header */}
          <motion.div
            className="flex flex-col sm:flex-row sm:items-center justify-between gap-3 mb-6 sm:mb-8"
            variants={fadeUp}
            initial="hidden"
            animate="visible"
            transition={{ duration: TIMING.normal }}
          >
            <div>
              <h1 className="text-2xl font-semibold text-[#f5f7fb]">Projetos</h1>
              <p className="text-sm text-[rgba(245,247,251,0.6)] mt-1">
                Gerencie os projetos da sua organizacao
              </p>
            </div>
            <motion.button
              onClick={() => {
                setEditingProject(null);
                setIsModalOpen(true);
              }}
              className="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-[#4A9FFF] to-[#3C7BFF] text-white rounded-lg font-medium text-sm hover:opacity-90 transition-opacity"
              whileHover={{ scale: 1.02 }}
              whileTap={{ scale: 0.98 }}
            >
              <Plus className="w-4 h-4" />
              Novo Projeto
            </motion.button>
          </motion.div>

          {/* Tabs */}
          <div className="flex gap-4 mb-6 relative">
            <button
              onClick={() => setShowArchived(false)}
              className={`relative px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                !showArchived
                  ? 'text-[#4A9FFF]'
                  : 'text-[rgba(245,247,251,0.6)] hover:text-[#f5f7fb]'
              }`}
            >
              {!showArchived && (
                <motion.div
                  layoutId="project-tab"
                  className="absolute inset-0 bg-[rgba(74,159,255,0.2)] rounded-lg"
                  transition={SPRING.snappy}
                />
              )}
              <span className="relative z-10">
                <FolderOpen className="w-4 h-4 inline-block mr-2" />
                Ativos ({activeProjects.length})
              </span>
            </button>
            <button
              onClick={() => setShowArchived(true)}
              className={`relative px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
                showArchived
                  ? 'text-[#4A9FFF]'
                  : 'text-[rgba(245,247,251,0.6)] hover:text-[#f5f7fb]'
              }`}
            >
              {showArchived && (
                <motion.div
                  layoutId="project-tab"
                  className="absolute inset-0 bg-[rgba(74,159,255,0.2)] rounded-lg"
                  transition={SPRING.snappy}
                />
              )}
              <span className="relative z-10">
                <Archive className="w-4 h-4 inline-block mr-2" />
                Arquivados ({archivedProjects.length})
              </span>
            </button>
          </div>

          {/* Error Message */}
          {error && (
            <div className="mb-4 p-4 bg-[rgba(255,107,122,0.1)] border border-[rgba(255,107,122,0.3)] rounded-lg text-[#FF6B7A]">
              {error}
            </div>
          )}

          {/* Loading State */}
          {isLoading && projects.length === 0 && (
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
              {[...Array(6)].map((_, i) => (
                <div key={i} className="bg-[rgba(26,29,46,0.6)] border border-[rgba(255,255,255,0.06)] rounded-xl p-4 space-y-3">
                  <SkeletonShimmer width="40%" height={12} rounded="rounded" />
                  <SkeletonShimmer width="80%" height={10} rounded="rounded" />
                  <SkeletonShimmer width="60%" height={10} rounded="rounded" />
                </div>
              ))}
            </div>
          )}

          {/* Empty State */}
          <AnimatePresence mode="wait">
            {!isLoading && displayedProjects.length === 0 && (
              <motion.div
                className="text-center py-12"
                variants={fadeUp}
                initial="hidden"
                animate="visible"
                exit="exit"
                transition={{ duration: TIMING.normal }}
              >
                <FolderOpen className="w-12 h-12 text-[rgba(245,247,251,0.2)] mx-auto mb-4 animate-float" />
                <p className="text-[rgba(245,247,251,0.4)]">
                  {showArchived ? 'Nenhum projeto arquivado' : 'Nenhum projeto criado ainda'}
                </p>
                {!showArchived && (
                  <button
                    onClick={() => setIsModalOpen(true)}
                    className="mt-4 text-[#4A9FFF] hover:underline text-sm"
                  >
                    Criar primeiro projeto
                  </button>
                )}
              </motion.div>
            )}
          </AnimatePresence>

          {/* Projects Grid */}
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
                >
                  <ProjectCard
                    project={project}
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

      {/* Modal */}
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
