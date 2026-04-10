/**
 * Projects — Manager/Admin page listing all org projects as cards.
 * Click a card → ProjectDetail page with kanban board.
 */

import { useState, useEffect, useCallback } from 'react';
import { motion } from 'motion/react';
import { Navigate, useNavigate } from 'react-router-dom';
import { FolderKanban, Plus, Loader2, Archive, ArchiveRestore, Trash2, Users, ChevronRight } from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { usePermissions } from '../hooks/usePermissions';
import {
  listProjects,
  createProject,
  archiveProject,
  reactivateProject,
  deleteProject,
  type ProjectItem,
} from '../services/projectsApi';

const PROJECT_COLORS = [
  '#8B5CF6', '#22D3EE', '#05df72', '#fbbf24', '#f87171',
  '#38bdf8', '#a78bfa', '#f472b6', '#60a5fa', '#4ade80',
];

export default function Projects() {
  const user = useAuthStore((s) => s.user);
  const { canManageTeam } = usePermissions();
  const navigate = useNavigate();

  const [tab, setTab] = useState<'active' | 'archived'>('active');
  const [projects, setProjects] = useState<ProjectItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [confirmDeleteId, setConfirmDeleteId] = useState<string | null>(null);

  if (!canManageTeam) {
    return <Navigate to="/" replace />;
  }

  const fetchProjects = useCallback(async () => {
    setLoading(true);
    try {
      const res = await listProjects(tab === 'active');
      setProjects(res.projects ?? []);
    } catch (err) {
      console.error('[Projects] list failed', err);
    } finally {
      setLoading(false);
    }
  }, [tab]);

  useEffect(() => {
    fetchProjects();
  }, [fetchProjects]);

  const handleArchive = async (id: string) => {
    try {
      await archiveProject(id);
      await fetchProjects();
    } catch (err) {
      console.error(err);
    }
  };

  const handleReactivate = async (id: string) => {
    try {
      await reactivateProject(id);
      await fetchProjects();
    } catch (err) {
      console.error(err);
    }
  };

  const handleDelete = async (id: string) => {
    try {
      await deleteProject(id);
      setConfirmDeleteId(null);
      await fetchProjects();
    } catch (err) {
      console.error(err);
    }
  };

  return (
    <div className="flex h-screen bg-transparent overflow-hidden pt-[52px] md:pt-0">
      <WebSidebar />

      <main className="flex-1 flex flex-col min-w-0 min-h-0">
        {/* Header */}
        <div className="px-4 lg:px-5 pt-4 pb-2 flex-shrink-0">
          <header className="flex items-center justify-between">
            <div className="relative">
              <h1 className="text-[16px] sm:text-[20px] font-semibold text-[#f5f7fb] pb-2">Projetos</h1>
              <motion.div
                className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3)]"
                layoutId="web-projects-tab"
              />
              <p className="text-[11px] sm:text-[12px] text-[rgba(245,247,251,0.4)] mt-1">
                Quadros Kanban e atribuições · {user?.orgName}
              </p>
            </div>
            <motion.button
              onClick={() => setShowCreate(true)}
              whileHover={{ scale: 1.04 }}
              whileTap={{ scale: 0.97 }}
              className="flex items-center gap-2 h-9 px-4 rounded-[12px] bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white text-[12px] font-semibold shadow-[0_4px_12px_rgba(139,92,246,0.3)] hover:from-[#7c3aed] hover:to-[#6d28d9] transition-colors"
            >
              <Plus className="w-4 h-4" />
              Novo Projeto
            </motion.button>
          </header>
        </div>

        {/* Tabs */}
        <div className="px-4 lg:px-5 pb-3 flex-shrink-0">
          <div className="inline-flex items-center gap-1 p-1 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)]">
            {(['active', 'archived'] as const).map((t) => (
              <button
                key={t}
                onClick={() => setTab(t)}
                className={`px-3 py-1.5 rounded-[8px] text-[11px] font-semibold transition-colors ${
                  tab === t
                    ? 'bg-[rgba(139,92,246,0.2)] text-[#c4b5fd]'
                    : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)]'
                }`}
              >
                {t === 'active' ? 'Ativos' : 'Arquivados'}
              </button>
            ))}
          </div>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto px-4 lg:px-5 pb-4 min-h-0">
          {loading ? (
            <div className="flex items-center justify-center py-16">
              <Loader2 className="w-5 h-5 text-[#8B5CF6] animate-spin" />
            </div>
          ) : projects.length === 0 ? (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <div className="w-16 h-16 rounded-2xl bg-[rgba(139,92,246,0.08)] border border-[rgba(139,92,246,0.15)] flex items-center justify-center mb-4">
                <FolderKanban className="w-7 h-7 text-[rgba(139,92,246,0.5)]" />
              </div>
              <p className="text-[15px] font-medium text-[rgba(245,247,251,0.6)]">
                {tab === 'active' ? 'Nenhum projeto ativo' : 'Nenhum projeto arquivado'}
              </p>
              {tab === 'active' && (
                <p className="text-[12px] text-[rgba(245,247,251,0.35)] mt-1 max-w-[320px]">
                  Crie um novo projeto para começar a organizar tarefas em um quadro Kanban.
                </p>
              )}
            </div>
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
              {projects.map((project) => {
                const isConfirming = confirmDeleteId === project.id;
                return (
                  <motion.div
                    key={project.id}
                    layout
                    initial={{ opacity: 0, y: 8 }}
                    animate={{ opacity: 1, y: 0 }}
                    className="group relative rounded-xl overflow-hidden bg-gradient-to-br from-[rgba(26,29,46,0.6)] to-[rgba(17,19,28,0.6)] border border-[rgba(255,255,255,0.06)] hover:border-[rgba(255,255,255,0.12)] transition-colors"
                  >
                    {/* Color bar */}
                    <div className="h-1" style={{ backgroundColor: project.color }} />

                    <button
                      onClick={() => navigate(`/projects/${project.id}`)}
                      className="w-full text-left p-4"
                    >
                      <div className="flex items-start justify-between gap-2 mb-2">
                        <div className="flex items-center gap-2 min-w-0">
                          <div
                            className="w-7 h-7 rounded-lg flex items-center justify-center flex-shrink-0"
                            style={{ backgroundColor: `${project.color}20`, border: `1px solid ${project.color}40` }}
                          >
                            <FolderKanban className="w-3.5 h-3.5" style={{ color: project.color }} />
                          </div>
                          <h3 className="text-[14px] font-semibold text-[#f5f7fb] truncate">
                            {project.name}
                          </h3>
                        </div>
                        <ChevronRight className="w-4 h-4 text-[rgba(245,247,251,0.3)] group-hover:text-[rgba(245,247,251,0.6)] flex-shrink-0 mt-1" />
                      </div>

                      {project.description ? (
                        <p className="text-[11px] text-[rgba(245,247,251,0.45)] line-clamp-2 mb-3">
                          {project.description}
                        </p>
                      ) : (
                        <p className="text-[11px] text-[rgba(245,247,251,0.25)] italic mb-3">Sem descrição</p>
                      )}

                      <div className="flex items-center gap-3 text-[10px] text-[rgba(245,247,251,0.4)]">
                        <span className="flex items-center gap-1">
                          <Users className="w-3 h-3" />
                          Membros
                        </span>
                        <span>·</span>
                        <span>
                          Criado {new Date(project.createdAt).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })}
                        </span>
                      </div>
                    </button>

                    {/* Action row */}
                    <div className="px-4 pb-3 flex items-center gap-1.5 opacity-0 group-hover:opacity-100 transition-opacity">
                      {isConfirming ? (
                        <>
                          <button
                            onClick={(e) => { e.stopPropagation(); handleDelete(project.id); }}
                            className="flex-1 py-1 rounded-lg text-[9px] font-semibold bg-[rgba(248,113,113,0.15)] text-[#f87171] border border-[rgba(248,113,113,0.3)] hover:bg-[rgba(248,113,113,0.25)] transition-colors"
                          >
                            Confirmar excluir
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(null); }}
                            className="px-2 py-1 rounded-lg text-[9px] text-[rgba(245,247,251,0.4)] hover:bg-[rgba(255,255,255,0.06)]"
                          >
                            ✕
                          </button>
                        </>
                      ) : tab === 'active' ? (
                        <>
                          <button
                            onClick={(e) => { e.stopPropagation(); handleArchive(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
                          >
                            <Archive className="w-3 h-3" />
                            Arquivar
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(248,113,113,0.5)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.06)] transition-colors"
                          >
                            <Trash2 className="w-3 h-3" />
                            Excluir
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            onClick={(e) => { e.stopPropagation(); handleReactivate(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[#05df72] hover:bg-[rgba(5,223,114,0.06)] transition-colors"
                          >
                            <ArchiveRestore className="w-3 h-3" />
                            Reativar
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(248,113,113,0.5)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.06)] transition-colors"
                          >
                            <Trash2 className="w-3 h-3" />
                            Excluir
                          </button>
                        </>
                      )}
                    </div>
                  </motion.div>
                );
              })}
            </div>
          )}
        </div>
      </main>

      {showCreate && (
        <CreateProjectModal
          onClose={() => setShowCreate(false)}
          onCreated={async () => {
            setShowCreate(false);
            await fetchProjects();
          }}
        />
      )}
    </div>
  );
}

// ── Create Project Modal ─────────────────────────────────────────────────────

function CreateProjectModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [color, setColor] = useState(PROJECT_COLORS[0]);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    setSaving(true);
    setError(null);
    try {
      await createProject({ name: name.trim(), description: description.trim() || undefined, color });
      onCreated();
    } catch (err: any) {
      setError(err?.message || 'Falha ao criar projeto');
    } finally {
      setSaving(false);
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
        className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[420px] max-w-[90vw] bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl p-5"
      >
        <h3 className="text-[15px] font-semibold text-[#f5f7fb] mb-4">Novo Projeto</h3>
        <form onSubmit={handleSubmit}>
          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Nome</label>
          <input
            type="text"
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={255}
            placeholder="Ex: Website Redesign"
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Descrição</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={1000}
            rows={3}
            placeholder="Opcional"
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] resize-none"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">Cor</label>
          <div className="flex flex-wrap gap-2 mb-5">
            {PROJECT_COLORS.map((c) => (
              <button
                key={c}
                type="button"
                onClick={() => setColor(c)}
                className={`w-8 h-8 rounded-lg transition-transform ${color === c ? 'ring-2 ring-offset-2 ring-offset-[#0b0d14] scale-110' : 'hover:scale-105'}`}
                style={{ backgroundColor: c, boxShadow: color === c ? `0 0 0 2px ${c}` : undefined }}
              />
            ))}
          </div>

          {error && <p className="text-[11px] text-[#f87171] mb-3">{error}</p>}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg text-[12px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)]"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={saving || !name.trim()}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-[12px] font-semibold bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {saving && <Loader2 className="w-3 h-3 animate-spin" />}
              Criar
            </button>
          </div>
        </form>
      </motion.div>
    </>
  );
}
