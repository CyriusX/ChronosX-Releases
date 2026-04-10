/**
 * AddMemberModal — add/remove project members.
 * Shows existing members + a dropdown of org users not yet in the project.
 */

import { useState, useEffect } from 'react';
import { motion } from 'motion/react';
import { X, UserPlus, Loader2, Check, Trash2 } from 'lucide-react';
import { addProjectMember, removeProjectMember, type ProjectMember } from '../../services/projectsApi';
import { listMembers } from '../../services/memberApi';
import type { Member } from '@desktop/types/member';

export function AddMemberModal({
  projectId,
  existingMembers,
  onClose,
  onChanged,
}: {
  projectId: string;
  existingMembers: ProjectMember[];
  onClose: () => void;
  onChanged: () => Promise<void>;
}) {
  const [orgMembers, setOrgMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [addingUserId, setAddingUserId] = useState<string | null>(null);
  const [removingUserId, setRemovingUserId] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [query, setQuery] = useState('');

  useEffect(() => {
    (async () => {
      try {
        const res = await listMembers();
        setOrgMembers(res.members ?? []);
      } catch (err: any) {
        setError(err?.message || 'Falha ao carregar usuários');
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const existingUserIds = new Set(existingMembers.map((m) => m.userId));
  const availableMembers = orgMembers.filter(
    (u) => !existingUserIds.has(u.userId) && u.status !== 'Inactive',
  );
  const filtered = availableMembers.filter((u) => {
    if (!query.trim()) return true;
    const q = query.toLowerCase();
    return u.displayName.toLowerCase().includes(q) || u.email.toLowerCase().includes(q);
  });

  const handleAdd = async (userId: string) => {
    setAddingUserId(userId);
    setError(null);
    try {
      await addProjectMember(projectId, { userId, role: 'Member' });
      await onChanged();
    } catch (err: any) {
      setError(err?.message || 'Falha ao adicionar membro');
    } finally {
      setAddingUserId(null);
    }
  };

  const handleRemove = async (userId: string) => {
    setRemovingUserId(userId);
    setError(null);
    try {
      await removeProjectMember(projectId, userId);
      await onChanged();
    } catch (err: any) {
      setError(err?.message || 'Falha ao remover membro');
    } finally {
      setRemovingUserId(null);
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
        className="fixed z-50 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[460px] max-w-[92vw] max-h-[80vh] bg-[#0b0d14] border border-[rgba(255,255,255,0.1)] rounded-xl shadow-2xl flex flex-col"
      >
        <div className="flex items-center justify-between p-5 pb-3 flex-shrink-0">
          <h3 className="text-[15px] font-semibold text-[#f5f7fb]">Membros do Projeto</h3>
          <button onClick={onClose} className="p-1 rounded-lg hover:bg-[rgba(255,255,255,0.06)]">
            <X className="w-4 h-4 text-[rgba(245,247,251,0.5)]" />
          </button>
        </div>

        <div className="px-5 flex-1 overflow-y-auto min-h-0">
          {/* Existing members */}
          {existingMembers.length > 0 && (
            <div className="mb-4">
              <div className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-2">
                Atuais ({existingMembers.length})
              </div>
              <div className="space-y-1">
                {existingMembers.map((m) => (
                  <div
                    key={m.id}
                    className="flex items-center gap-3 p-2 rounded-lg bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.05)]"
                  >
                    <div className="w-8 h-8 rounded-full bg-gradient-to-br from-[#8B5CF6] to-[#3B82F6] flex items-center justify-center text-[11px] font-bold text-white flex-shrink-0">
                      {m.displayName.charAt(0).toUpperCase()}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className="text-[12px] font-medium text-[#f5f7fb] truncate">{m.displayName}</p>
                      <p className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">{m.email}</p>
                    </div>
                    <button
                      onClick={() => handleRemove(m.userId)}
                      disabled={removingUserId === m.userId}
                      className="p-1.5 rounded-lg text-[rgba(248,113,113,0.6)] hover:text-[#f87171] hover:bg-[rgba(248,113,113,0.08)] disabled:opacity-40 transition-colors"
                      title="Remover"
                    >
                      {removingUserId === m.userId ? (
                        <Loader2 className="w-3.5 h-3.5 animate-spin" />
                      ) : (
                        <Trash2 className="w-3.5 h-3.5" />
                      )}
                    </button>
                  </div>
                ))}
              </div>
            </div>
          )}

          {/* Search + available list */}
          <div className="mb-3">
            <input
              type="text"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder="Buscar usuários..."
              className="w-full px-3 py-2 rounded-lg bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[12px] text-[#f5f7fb] placeholder-[rgba(245,247,251,0.3)] focus:outline-none focus:border-[rgba(139,92,246,0.4)]"
            />
          </div>

          {loading ? (
            <div className="flex items-center justify-center py-8">
              <Loader2 className="w-4 h-4 text-[#8B5CF6] animate-spin" />
            </div>
          ) : filtered.length === 0 ? (
            <p className="text-[11px] text-[rgba(245,247,251,0.4)] text-center py-6">
              {query ? 'Nenhum usuário encontrado' : 'Todos os usuários já são membros'}
            </p>
          ) : (
            <div className="space-y-1 mb-3">
              <div className="text-[10px] uppercase tracking-wider text-[rgba(245,247,251,0.4)] mb-2">
                Adicionar
              </div>
              {filtered.map((u) => (
                <button
                  key={u.userId}
                  onClick={() => handleAdd(u.userId)}
                  disabled={addingUserId === u.userId}
                  className="w-full flex items-center gap-3 p-2 rounded-lg hover:bg-[rgba(139,92,246,0.08)] border border-transparent hover:border-[rgba(139,92,246,0.15)] disabled:opacity-40 transition-colors text-left"
                >
                  <div className="w-8 h-8 rounded-full bg-[rgba(255,255,255,0.06)] flex items-center justify-center text-[11px] font-bold text-[rgba(245,247,251,0.7)] flex-shrink-0">
                    {u.displayName.charAt(0).toUpperCase()}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className="text-[12px] font-medium text-[#f5f7fb] truncate">{u.displayName}</p>
                    <p className="text-[10px] text-[rgba(245,247,251,0.4)] truncate">{u.email} · {u.role}</p>
                  </div>
                  {addingUserId === u.userId ? (
                    <Loader2 className="w-3.5 h-3.5 text-[#c4b5fd] animate-spin flex-shrink-0" />
                  ) : (
                    <UserPlus className="w-3.5 h-3.5 text-[rgba(139,92,246,0.6)] flex-shrink-0" />
                  )}
                </button>
              ))}
            </div>
          )}

          {error && <p className="text-[11px] text-[#f87171] mb-3">{error}</p>}
        </div>

        <div className="flex justify-end p-5 pt-3 flex-shrink-0 border-t border-[rgba(255,255,255,0.04)]">
          <button
            onClick={onClose}
            className="flex items-center gap-2 px-4 py-2 rounded-lg text-[12px] font-semibold bg-[rgba(139,92,246,0.15)] text-[#c4b5fd] border border-[rgba(139,92,246,0.3)] hover:bg-[rgba(139,92,246,0.25)] transition-colors"
          >
            <Check className="w-3.5 h-3.5" />
            Pronto
          </button>
        </div>
      </motion.div>
    </>
  );
}
