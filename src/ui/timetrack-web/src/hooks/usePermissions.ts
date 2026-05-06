/**
 * usePermissions — Web version that imports from web authStore
 */

import { useAuthStore } from '../stores/authStore';

export function usePermissions() {
  const { user } = useAuthStore();
  const role = user?.role;

  const isAdmin = (): boolean => role === 'Admin';
  const isManager = (): boolean => role === 'Gestor';
  const isColaborador = (): boolean => role === 'Colaborador';

  const canManageTeam = (): boolean => isAdmin() || isManager();
  const canViewOrgPolicies = (): boolean => isAdmin() || isManager();
  const canInviteMembers = (): boolean => isAdmin() || isManager();
  const canRemoveMembers = (): boolean => isAdmin();
  const canChangeMemberRole = (): boolean => isAdmin();
  const canEditOrgPolicies = (): boolean => isAdmin() || isManager();

  return {
    role,
    isAdmin: isAdmin(),
    isManager: isManager(),
    isColaborador: isColaborador(),
    canManageTeam: canManageTeam(),
    canViewOrgPolicies: canViewOrgPolicies(),
    canEditOrgPolicies: canEditOrgPolicies(),
    canInviteMembers: canInviteMembers(),
    canRemoveMembers: canRemoveMembers(),
    canChangeMemberRole: canChangeMemberRole(),
  };
}
