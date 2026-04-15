import { useState, useEffect } from 'react';
import { UserPlus, Loader2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
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
  const { t } = useTranslation();
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
    notify.success(t('settings.members.inviteSuccess'));
    setShowInviteForm(false);
  };

  const handleToggleStatus = async (member: Parameters<typeof toggleStatus>[0]) => {
    try {
      await toggleStatus(member);
      const action = member.status === 'Active' ? t('settings.members.deactivated') : t('settings.members.activated');
      notify.success(`${t('settings.members.member')} ${action}`);
    } catch {
      notify.error(t('common.error'));
    }
  };

  const handleChangeRole = async (member: Parameters<typeof changeRole>[0], role: Parameters<typeof changeRole>[1]) => {
    try {
      await changeRole(member, role);
      notify.success(t('settings.members.roleUpdated'));
    } catch {
      notify.error(t('common.error'));
    }
  };

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12">
        <Loader2 className="w-6 h-6 animate-spin text-[#8B5CF6]" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-[20px] font-semibold text-[#f5f7fb]">{t('settings.members.title')}</h2>
          <p className="text-[13px] text-[rgba(245,247,251,0.5)] mt-1">
            {t('settings.members.subtitle')}
          </p>
        </div>
        {canInviteMembers && (
          <button
            onClick={() => setShowInviteForm(!showInviteForm)}
            className="flex items-center gap-2 px-4 py-2 bg-gradient-to-r from-[#8B5CF6] to-[#22D3EE] rounded-lg text-[13px] font-medium text-white hover:opacity-90 transition-opacity"
          >
            <UserPlus className="w-4 h-4" />
            {t('settings.members.invite')}
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
