/**
 * Projects — Manager/Admin page listing all org projects as cards.
 * Click a card → ProjectDetail page with kanban board.
 */

import { useState, useEffect, useCallback } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { Navigate, useNavigate } from 'react-router-dom';
import { FolderKanban, Plus, Loader2, Archive, ArchiveRestore, Trash2, Users, ChevronRight, DollarSign } from 'lucide-react';
import { WebSidebar } from '../components/WebSidebar';
import { useAuthStore } from '../stores/authStore';
import { usePermissions } from '../hooks/usePermissions';
import {
  listProjects,
  createProject,
  updateProject as updateProjectApi,
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
  const { t } = useTranslation();
  const user = useAuthStore((s) => s.user);
  const { canManageTeam } = usePermissions();
  const navigate = useNavigate();

  const [tab, setTab] = useState<'active' | 'archived'>('active');
  const [projects, setProjects] = useState<ProjectItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [showEdit, setShowEdit] = useState(false);
  const [editingProject, setEditingProject] = useState<ProjectItem | null>(null);
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

  const handleEdit = (project: ProjectItem) => {
    setEditingProject(project);
    setShowEdit(true);
    setConfirmDeleteId(null);
  };

  const handleUpdate = async (name: string, description: string, color: string, isBillable: boolean, currency: string | null, hourlyRate: number | null) => {
    if (!editingProject) return;
    try {
      await updateProjectApi(editingProject.id, {
        name,
        description: description || undefined,
        color,
        isBillable,
        currency: isBillable ? (currency ?? undefined) : undefined,
        hourlyRate: isBillable ? (hourlyRate ?? undefined) : undefined,
      });
      setShowEdit(false);
      setEditingProject(null);
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
              <h1 className="text-[16px] sm:text-[20px] font-semibold text-[#f5f7fb] pb-2">{t('projects.title')}</h1>
              <motion.div
                className="absolute bottom-0 left-0 right-0 h-[2px] bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-full shadow-[0px_10px_15px_0px_rgba(139,92,246,0.3)]"
                layoutId="web-projects-tab"
              />
              <p className="text-[11px] sm:text-[12px] text-[rgba(245,247,251,0.4)] mt-1">
                {t('projects.boardDesc')} {user?.orgName}
              </p>
            </div>
            <motion.button
              onClick={() => setShowCreate(true)}
              whileHover={{ scale: 1.04 }}
              whileTap={{ scale: 0.97 }}
              className="flex items-center gap-2 h-9 px-4 rounded-[12px] bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white text-[12px] font-semibold shadow-[0_4px_12px_rgba(139,92,246,0.3)] hover:from-[#7c3aed] hover:to-[#6d28d9] transition-colors"
            >
              <Plus className="w-4 h-4" />
              {t('projects.newProject')}
            </motion.button>
          </header>
        </div>

        {/* Tabs */}
        <div className="px-4 lg:px-5 pb-3 flex-shrink-0">
          <div className="inline-flex items-center gap-1 p-1 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)]">
            {(['active', 'archived'] as const).map((tabKey) => (
              <button
                key={tabKey}
                onClick={() => setTab(tabKey)}
                className={`px-3 py-1.5 rounded-[8px] text-[11px] font-semibold transition-colors ${
                  tab === tabKey
                    ? 'bg-[rgba(139,92,246,0.2)] text-[#c4b5fd]'
                    : 'text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)]'
                }`}
              >
                {tabKey === 'active' ? t('projects.active') : t('projects.archived')}
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
                {tab === 'active' ? t('projects.noActiveProjects') : t('projects.noArchivedProjects')}
              </p>
              {tab === 'active' && (
                <p className="text-[12px] text-[rgba(245,247,251,0.35)] mt-1 max-w-[320px]">
                  {t('projects.createFirstHint')}
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
                        <p className="text-[11px] text-[rgba(245,247,251,0.25)] italic mb-3">{t('projects.noDescription')}</p>
                      )}

                      <div className="flex items-center gap-3 text-[10px] text-[rgba(245,247,251,0.4)]">
                        <span className="flex items-center gap-1">
                          <Users className="w-3 h-3" />
                          {t('projects.members')}
                        </span>
                        <span>·</span>
                        <span>
                          {t('projects.created')} {new Date(project.createdAt).toLocaleDateString('pt-BR', { day: '2-digit', month: 'short' })}
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
                            {t('projects.confirmDelete')}
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
                            onClick={(e) => { e.stopPropagation(); handleEdit(project); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
                          >
                            {t('common.edit')}
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); handleArchive(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(245,247,251,0.5)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.04)] transition-colors"
                          >
                            <Archive className="w-3 h-3" />
                            {t('projects.archive')}
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(248,113,113,0.5)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.06)] transition-colors"
                          >
                            <Trash2 className="w-3 h-3" />
                            {t('common.delete')}
                          </button>
                        </>
                      ) : (
                        <>
                          <button
                            onClick={(e) => { e.stopPropagation(); handleReactivate(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[#05df72] hover:bg-[rgba(5,223,114,0.06)] transition-colors"
                          >
                            <ArchiveRestore className="w-3 h-3" />
                            {t('projects.reactivate')}
                          </button>
                          <button
                            onClick={(e) => { e.stopPropagation(); setConfirmDeleteId(project.id); }}
                            className="flex items-center gap-1 px-2 py-1 rounded-lg text-[9px] text-[rgba(248,113,113,0.5)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.06)] transition-colors"
                          >
                            <Trash2 className="w-3 h-3" />
                            {t('common.delete')}
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

      {showEdit && editingProject && (
        <EditProjectModal
          project={editingProject}
          onClose={() => {
            setShowEdit(false);
            setEditingProject(null);
          }}
          onUpdated={handleUpdate}
        />
      )}

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

// ── Edit Project Modal ─────────────────────────────────────────────────────

function EditProjectModal({ project, onClose, onUpdated }: { project: ProjectItem; onClose: () => void; onUpdated: (name: string, description: string, color: string, isBillable: boolean, currency: string | null, hourlyRate: number | null) => void }) {
  const { t } = useTranslation();
  const [name, setName] = useState(project.name);
  const [description, setDescription] = useState(project.description || '');
  const [color, setColor] = useState(project.color);
  const [isBillable, setIsBillable] = useState(project.isBillable);
  const [currency, setCurrency] = useState(project.currency || 'USD');
  const [hourlyRate, setHourlyRate] = useState(project.hourlyRate?.toString() || '');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const CURRENCIES = ['USD', 'BRL', 'EUR', 'GBP', 'CAD', 'AUD', 'JPY'] as const;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    if (isBillable && (!currency.trim() || !hourlyRate.trim() || parseFloat(hourlyRate) <= 0)) return;
    setSaving(true);
    setError(null);
    try {
      onUpdated(name.trim(), description.trim(), color, isBillable, isBillable ? currency : null, isBillable ? parseFloat(hourlyRate) : null);
    } catch (err: any) {
      setError(err?.message || t('projects.updateFailed'));
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
        <h3 className="text-[15px] font-semibold text-[#f5f7fb] mb-4">{t('projects.editProject')}</h3>
        <form onSubmit={handleSubmit}>
          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.name')}</label>
          <input
            type="text"
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={255}
            placeholder={t('projects.namePlaceholder')}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.description')}</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={1000}
            rows={3}
            placeholder={t('common.optional')}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] resize-none"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.color')}</label>
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

          {/* Billable Toggle */}
          <div className="flex items-center justify-between py-2 mb-4">
            <label className="flex items-center gap-2 text-[12px] font-medium text-[rgba(245,247,251,0.7)] cursor-pointer">
              <div className="relative">
                <input
                  type="checkbox"
                  checked={isBillable}
                  onChange={(e) => setIsBillable(e.target.checked)}
                  className="sr-only peer"
                />
                <div className={`w-9 h-5 rounded-full transition-colors ${
                  isBillable ? 'bg-[#8B5CF6]' : 'bg-[rgba(255,255,255,0.1)]'
                }`}>
                  <div className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full transition-all duration-200 ${
                    isBillable ? 'translate-x-4 bg-white' : 'translate-x-0 bg-[rgba(255,255,255,0.4)]'
                  }`} />
                </div>
              </div>
              <span>{t('projects.billable')}</span>
            </label>
          </div>

          {/* Billable Fields */}
          {isBillable && (
            <>
              {/* Currency */}
              <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.currency')}</label>
              <div className="grid grid-cols-4 gap-2 mb-5">
                {CURRENCIES.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => setCurrency(c)}
                    className={`py-2 px-3 rounded-lg text-[12px] font-semibold transition-all ${
                      currency === c
                        ? 'bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white'
                        : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.9)]'
                    }`}
                  >
                    {c}
                  </button>
                ))}
              </div>

              {/* Hourly Rate */}
              <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.hourlyRate')}</label>
              <div className="relative mb-5">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 flex items-center text-[rgba(245,247,251,0.4)]">
                  <DollarSign className="w-4 h-4" />
                </div>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={hourlyRate}
                  onChange={(e) => setHourlyRate(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
                  placeholder="40.00"
                />
              </div>
            </>
          )}

          {error && <p className="text-[11px] text-[#f87171] mb-3">{error}</p>}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg text-[12px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)]"
            >
              {t('common.cancel')}
            </button>
            <button
              type="submit"
              disabled={saving || !name.trim() || (isBillable && (!currency.trim() || !hourlyRate.trim() || parseFloat(hourlyRate) <= 0))}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-[12px] font-semibold bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {saving && <Loader2 className="w-3 h-3 animate-spin" />}
              {t('common.save')}
            </button>
          </div>
        </form>
      </motion.div>
    </>
  );
}

// ── Create Project Modal ─────────────────────────────────────────────────────

function CreateProjectModal({ onClose, onCreated }: { onClose: () => void; onCreated: () => void }) {
  const { t } = useTranslation();
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [color, setColor] = useState(PROJECT_COLORS[0]);
  const [isBillable, setIsBillable] = useState(false);
  const [currency, setCurrency] = useState('USD');
  const [hourlyRate, setHourlyRate] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const CURRENCIES = ['USD', 'BRL', 'EUR', 'GBP', 'CAD', 'AUD', 'JPY'] as const;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) return;
    if (isBillable && (!currency.trim() || !hourlyRate.trim() || parseFloat(hourlyRate) <= 0)) return;
    setSaving(true);
    setError(null);
    try {
      await createProject({
        name: name.trim(),
        description: description.trim() || undefined,
        color,
        isBillable,
        currency: isBillable ? currency : undefined,
        hourlyRate: isBillable ? parseFloat(hourlyRate) : undefined,
      });
      onCreated();
    } catch (err: any) {
      setError(err?.message || t('projects.createFailed'));
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
        <h3 className="text-[15px] font-semibold text-[#f5f7fb] mb-4">{t('projects.newProject')}</h3>
        <form onSubmit={handleSubmit}>
          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.name')}</label>
          <input
            type="text"
            autoFocus
            value={name}
            onChange={(e) => setName(e.target.value)}
            maxLength={255}
            placeholder={t('projects.namePlaceholder')}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.description')}</label>
          <textarea
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            maxLength={1000}
            rows={3}
            placeholder={t('common.optional')}
            className="w-full px-3 py-2 mb-3 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)] resize-none"
          />

          <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.color')}</label>
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

          {/* Billable Toggle */}
          <div className="flex items-center justify-between py-2 mb-4">
            <label className="flex items-center gap-2 text-[12px] font-medium text-[rgba(245,247,251,0.7)] cursor-pointer">
              <div className="relative">
                <input
                  type="checkbox"
                  checked={isBillable}
                  onChange={(e) => setIsBillable(e.target.checked)}
                  className="sr-only peer"
                />
                <div className={`w-9 h-5 rounded-full transition-colors ${
                  isBillable ? 'bg-[#8B5CF6]' : 'bg-[rgba(255,255,255,0.1)]'
                }`}>
                  <div className={`absolute top-0.5 left-0.5 w-4 h-4 rounded-full transition-all duration-200 ${
                    isBillable ? 'translate-x-4 bg-white' : 'translate-x-0 bg-[rgba(255,255,255,0.4)]'
                  }`} />
                </div>
              </div>
              <span>{t('projects.billable')}</span>
            </label>
          </div>

          {/* Billable Fields */}
          {isBillable && (
            <>
              {/* Currency */}
              <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.currency')}</label>
              <div className="grid grid-cols-4 gap-2 mb-5">
                {CURRENCIES.map((c) => (
                  <button
                    key={c}
                    type="button"
                    onClick={() => setCurrency(c)}
                    className={`py-2 px-3 rounded-lg text-[12px] font-semibold transition-all ${
                      currency === c
                        ? 'bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white'
                        : 'bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.9)]'
                    }`}
                  >
                    {c}
                  </button>
                ))}
              </div>

              {/* Hourly Rate */}
              <label className="block text-[10px] text-[rgba(245,247,251,0.5)] uppercase tracking-wider mb-1.5">{t('projects.hourlyRate')}</label>
              <div className="relative mb-5">
                <div className="absolute left-3 top-1/2 -translate-y-1/2 flex items-center text-[rgba(245,247,251,0.4)]">
                  <DollarSign className="w-4 h-4" />
                </div>
                <input
                  type="number"
                  step="0.01"
                  min="0"
                  value={hourlyRate}
                  onChange={(e) => setHourlyRate(e.target.value)}
                  className="w-full pl-9 pr-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
                  placeholder="40.00"
                />
              </div>
            </>
          )}

          {error && <p className="text-[11px] text-[#f87171] mb-3">{error}</p>}

          <div className="flex justify-end gap-2">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 rounded-lg text-[12px] text-[rgba(245,247,251,0.5)] hover:bg-[rgba(255,255,255,0.06)]"
            >
              {t('common.cancel')}
            </button>
            <button
              type="submit"
              disabled={saving || !name.trim() || (isBillable && (!currency.trim() || !hourlyRate.trim() || parseFloat(hourlyRate) <= 0))}
              className="flex items-center gap-2 px-4 py-2 rounded-lg text-[12px] font-semibold bg-gradient-to-r from-[#8B5CF6] to-[#7c3aed] text-white disabled:opacity-40 disabled:cursor-not-allowed"
            >
              {saving && <Loader2 className="w-3 h-3 animate-spin" />}
              {t('common.create')}
            </button>
          </div>
        </form>
      </motion.div>
    </>
  );
}
