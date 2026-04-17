import { useCallback, useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { motion } from 'motion/react';
import { X, Search, Plus, Trash2, Users } from 'lucide-react';
import { listMembers } from '../../services/memberApi';
import type { Member } from '../../types/member';
import {
  addProjectMember,
  listProjectMembers,
  removeProjectMember,
  type ProjectMember,
} from '../../services/projectsApi';
import { useNotifications } from '../../stores/uiStore';

interface ProjectMembersModalProps {
  projectId: string;
  projectName?: string;
  onClose: () => void;
  onChanged?: () => void;
}

export function ProjectMembersModal({ projectId, projectName, onClose, onChanged }: ProjectMembersModalProps) {
  const { t } = useTranslation();
  const { notify } = useNotifications();

  const [loading, setLoading] = useState(true);
  const [members, setMembers] = useState<ProjectMember[]>([]);
  const [orgMembers, setOrgMembers] = useState<Member[]>([]);
  const [query, setQuery] = useState('');
  const [newRole, setNewRole] = useState<'member' | 'owner'>('member');
  const [addingUserId, setAddingUserId] = useState<string | null>(null);
  const [removingUserId, setRemovingUserId] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [proj, org] = await Promise.all([
        listProjectMembers(projectId).catch(() => ({ members: [], totalCount: 0 })),
        listMembers().catch(() => ({ members: [], totalCount: 0 })),
      ]);
      setMembers(proj.members ?? []);
      setOrgMembers(org.members ?? []);
    } finally {
      setLoading(false);
    }
  }, [projectId]);

  useEffect(() => {
    load();
    const handleKey = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    document.addEventListener('keydown', handleKey);
    return () => document.removeEventListener('keydown', handleKey);
  }, [load, onClose]);

  const memberIds = useMemo(() => new Set(members.map((m) => m.userId)), [members]);

  const availableUsers = useMemo(() => {
    const q = query.trim().toLowerCase();
    return orgMembers
      .filter((m) => m.status === 'Active')
      .filter((m) => !memberIds.has(m.userId))
      .filter((m) => !q || m.displayName.toLowerCase().includes(q) || m.email.toLowerCase().includes(q));
  }, [orgMembers, memberIds, query]);

  async function handleAdd(userId: string) {
    setAddingUserId(userId);
    try {
      await addProjectMember(projectId, userId, newRole);
      notify.success(t('common.success'), t('members.addSuccess'));
      await load();
      onChanged?.();
    } catch (err) {
      console.error('[ProjectMembersModal] add failed', err);
      notify.error(t('common.error'), t('members.addError'));
    } finally {
      setAddingUserId(null);
    }
  }

  async function handleRemove(userId: string) {
    setRemovingUserId(userId);
    try {
      await removeProjectMember(projectId, userId);
      notify.success(t('common.success'), t('members.removeSuccess'));
      await load();
      onChanged?.();
    } catch (err) {
      console.error('[ProjectMembersModal] remove failed', err);
      notify.error(t('common.error'), t('members.removeError'));
    } finally {
      setRemovingUserId(null);
    }
  }

  return (
    <>
      {/* Backdrop */}
      <motion.div
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        className="fixed inset-0 bg-[rgba(0,0,0,0.55)] backdrop-blur-sm z-40"
        onClick={onClose}
      />

      {/* Modal */}
      <motion.div
        initial={{ opacity: 0, y: 8, scale: 0.98 }}
        animate={{ opacity: 1, y: 0, scale: 1 }}
        exit={{ opacity: 0, y: 8, scale: 0.98 }}
        transition={{ type: 'spring', stiffness: 260, damping: 26 }}
        className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-[820px] max-w-[calc(100vw-24px)] bg-[rgb(14,16,26)] border border-[rgba(255,255,255,0.08)] rounded-2xl shadow-2xl z-50 overflow-hidden"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between gap-3 px-5 py-4 border-b border-[rgba(255,255,255,0.06)]">
          <div className="flex items-center gap-2 min-w-0">
            <div className="w-9 h-9 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.06)] flex items-center justify-center flex-shrink-0">
              <Users className="w-4 h-4 text-[rgba(245,247,251,0.65)]" />
            </div>
            <div className="min-w-0">
              <div className="text-[14px] font-semibold text-[#f5f7fb] truncate">{t('members.projectMembers')}</div>
              {projectName && (
                <div className="text-[11px] text-[rgba(245,247,251,0.4)] truncate">{projectName}</div>
              )}
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.9)] hover:bg-[rgba(255,255,255,0.06)] transition-colors"
            aria-label={t('common.close')}
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Body */}
        <div className="p-5 grid grid-cols-1 md:grid-cols-2 gap-5">
          {/* Current members */}
          <div className="rounded-2xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] overflow-hidden">
            <div className="px-4 py-3 border-b border-[rgba(255,255,255,0.06)]">
              <div className="text-[12px] font-semibold text-[rgba(245,247,251,0.8)]">{t('members.current')}</div>
            </div>
            <div className="max-h-[420px] overflow-y-auto">
              {loading ? (
                <div className="p-4 text-[12px] text-[rgba(245,247,251,0.45)]">{t('common.loading')}</div>
              ) : members.length === 0 ? (
                <div className="p-4 text-[12px] text-[rgba(245,247,251,0.35)]">{t('tasks.noMembersWarning')}</div>
              ) : (
                <div className="divide-y divide-[rgba(255,255,255,0.06)]">
                  {members.map((m) => (
                    <div key={m.userId} className="px-4 py-3 flex items-center gap-3">
                      <div className="min-w-0 flex-1">
                        <div className="text-[12px] text-[#f5f7fb] truncate">{m.displayName}</div>
                        <div className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">
                          {m.email} · {m.role}
                        </div>
                      </div>
                      <button
                        onClick={() => handleRemove(m.userId)}
                        disabled={removingUserId === m.userId}
                        className="w-9 h-9 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(248,113,113,0.12)] hover:border-[rgba(248,113,113,0.25)] transition-colors disabled:opacity-60"
                        title={t('members.removeTooltip')}
                      >
                        <Trash2 className="w-4 h-4 text-[#f87171]" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          </div>

          {/* Add members */}
          <div className="rounded-2xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)] overflow-hidden">
            <div className="px-4 py-3 border-b border-[rgba(255,255,255,0.06)] flex items-center justify-between gap-3">
              <div className="text-[12px] font-semibold text-[rgba(245,247,251,0.8)]">{t('members.addSection')}</div>
              <select
                value={newRole}
                onChange={(e) => setNewRole((e.target.value as 'member' | 'owner') ?? 'member')}
                className="h-8 px-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.1)] text-[11px] text-[rgba(245,247,251,0.75)] outline-none"
              >
                <option value="member">Member</option>
                <option value="owner">Owner</option>
              </select>
            </div>

            <div className="p-4">
              <div className="relative mb-3">
                <Search className="w-4 h-4 text-[rgba(245,247,251,0.35)] absolute left-3 top-1/2 -translate-y-1/2" />
                <input
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  placeholder={t('members.searchPlaceholder')}
                  className="w-full h-10 pl-10 pr-3 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[12px] text-[rgba(245,247,251,0.85)] placeholder:text-[rgba(245,247,251,0.3)] outline-none focus:border-[rgba(139,92,246,0.35)]"
                />
              </div>

              <div className="max-h-[370px] overflow-y-auto">
                {loading ? (
                  <div className="text-[12px] text-[rgba(245,247,251,0.45)]">{t('common.loading')}</div>
                ) : availableUsers.length === 0 ? (
                  <div className="text-[12px] text-[rgba(245,247,251,0.35)]">
                    {query.trim() ? t('members.noResults') : t('members.allMembers')}
                  </div>
                ) : (
                  <div className="space-y-2">
                    {availableUsers.map((u) => (
                      <div key={u.userId} className="flex items-center gap-3 px-3 py-2 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
                        <div className="min-w-0 flex-1">
                          <div className="text-[12px] text-[#f5f7fb] truncate">{u.displayName}</div>
                          <div className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">{u.email}</div>
                        </div>
                        <button
                          onClick={() => handleAdd(u.userId)}
                          disabled={addingUserId === u.userId}
                          className="h-9 px-3 rounded-xl bg-gradient-to-r from-[#4A9FFF] to-[#3C7BFF] text-white text-[11px] font-semibold shadow-[0_4px_14px_rgba(74,159,255,0.25)] hover:shadow-[0_6px_18px_rgba(74,159,255,0.35)] transition-shadow disabled:opacity-60 flex items-center gap-2"
                        >
                          <Plus className="w-3.5 h-3.5" />
                          {t('common.add')}
                        </button>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </motion.div>
    </>
  );
}
