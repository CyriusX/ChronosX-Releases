import { useState, useEffect } from 'react';
import { UserPlus, Loader2 } from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { useNotifications } from '../../stores/uiStore';
import { useMembers } from '../../hooks/useMembers';
import { usePermissions } from '../../hooks/usePermissions';
import { InviteForm } from './InviteForm';
import { MembersList } from './MembersList';

/**
 * MembersSection - Orquestração da aba de membros
 *
 * Responsabilidade única: compor componentes e gerenciar estado de UI
 *
 * SOLID:
 * - SRP: Apenas orquestração, delega lógica para useMembers e UI para subcomponentes
 * - OCP: Extensível via props e composição
 */
export function MembersSection() {
  const { user: currentUser } = useAuthStore();
  const { notify } = useNotifications();
  const { members, isLoading, loadMembers, invite, toggleStatus, changeRole } = useMembers();
  const { isAdmin, canInviteMembers } = usePermissions();

  const [showInviteForm, setShowInviteForm] = useState(false);

  useEffect(() => {
    loadMembers();
  }, [loadMembers]);

  const handleInvite = async (email: string, displayName: string, role: Parameters<typeof invite>[2]) => {
    await invite(email, displayName, role);
  };

  const handleInviteSuccess = () => {
    notify.success('Convite enviado com sucesso!');
    setShowInviteForm(false);
  };

  const handleToggleStatus = async (member: Parameters<typeof toggleStatus>[0]) => {
    try {
      await toggleStatus(member);
      const action = member.status === 'Active' ? 'desativado' : 'ativado';
      notify.success(`Membro ${action}`);
    } catch {
      notify.error('Erro ao atualizar status');
    }
  };

  const handleChangeRole = async (member: Parameters<typeof changeRole>[0], role: Parameters<typeof changeRole>[1]) => {
    try {
      await changeRole(member, role);
      notify.success('Role atualizada');
    } catch {
      notify.error('Erro ao atualizar role');
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="w-6 h-6 animate-spin text-[#4ad9ff]" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-[20px] font-semibold text-[#f5f7fb]">Membros</h2>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
            Gerencie os membros da sua organização
          </p>
        </div>
        {canInviteMembers && (
          <button
            onClick={() => setShowInviteForm(!showInviteForm)}
            className="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-[#4ad9ff] to-[#3c7bff] rounded-lg text-[13px] font-medium text-white hover:opacity-90 transition-opacity"
          >
            <UserPlus className="w-4 h-4" />
            Convidar
          </button>
        )}
      </div>

      {/* Invite Form */}
      {showInviteForm && (
        <InviteForm
          onInvite={handleInvite}
          onSuccess={handleInviteSuccess}
          onCancel={() => setShowInviteForm(false)}
        />
      )}

      {/* Members List */}
      <MembersList
        members={members}
        currentUserId={currentUser?.id}
        isAdmin={isAdmin}
        onToggleStatus={handleToggleStatus}
        onChangeRole={handleChangeRole}
      />
    </div>
  );
}
