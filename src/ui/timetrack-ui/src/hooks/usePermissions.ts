import { useAuthStore } from '../stores/authStore';

/**
 * usePermissions - Hook centralizado para RBAC
 *
 * Fornece funções helper para verificação de permissões
 * baseadas no role do usuário.
 *
 * Roles:
 * - Admin: Acesso total
 * - Gestor: Pode gerenciar equipe e ver políticas
 * - Colaborador: Apenas suas próprias atividades
 *
 * SOLID:
 * - SRP: Apenas verificação de permissões
 * - OCP: Novas permissões adicionadas via novas funções
 */
export function usePermissions() {
  const { user } = useAuthStore();
  const role = user?.role;

  /**
   * Verifica se o usuário é Admin
   */
  const isAdmin = (): boolean => role === 'Admin';

  /**
   * Verifica se o usuário é Gestor
   */
  const isManager = (): boolean => role === 'Gestor';

  /**
   * Verifica se o usuário é Colaborador
   */
  const isColaborador = (): boolean => role === 'Colaborador';

  /**
   * Verifica se pode gerenciar equipe (Admin ou Gestor)
   * - Ver aba Equipe no Dashboard
   * - Ver card "Equipe agora"
   * - Ver aba Membros em Configurações
   */
  const canManageTeam = (): boolean => isAdmin() || isManager();

  /**
   * Verifica se pode ver políticas da organização (Admin ou Gestor)
   * - Ver PolicyCards em Configurações
   */
  const canViewOrgPolicies = (): boolean => isAdmin() || isManager();

  /**
   * Verifica se pode convidar membros (Admin ou Gestor)
   */
  const canInviteMembers = (): boolean => isAdmin() || isManager();

  /**
   * Verifica se pode remover membros (apenas Admin)
   */
  const canRemoveMembers = (): boolean => isAdmin();

  /**
   * Verifica se pode alterar role de membros (apenas Admin)
   */
  const canChangeMemberRole = (): boolean => isAdmin();

  /**
   * Verifica se pode editar políticas da organização (apenas Admin)
   */
  const canEditOrgPolicies = (): boolean => isAdmin();

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
