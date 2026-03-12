import { useState, useCallback } from 'react';
import { useAuthStore } from '../stores/authStore';
import {
  listMembers,
  inviteMember,
  updateMemberStatus,
  updateMemberRole,
} from '../services/memberApi';
import type { Member, UserRole } from '../types/member';

interface UseMembersReturn {
  members: Member[];
  isLoading: boolean;
  error: string | null;
  loadMembers: () => Promise<void>;
  invite: (email: string, displayName: string, role: UserRole) => Promise<boolean>;
  toggleStatus: (member: Member) => Promise<boolean>;
  changeRole: (member: Member, role: UserRole) => Promise<boolean>;
}

export function useMembers(): UseMembersReturn {
  const { tokens } = useAuthStore();
  const accessToken = tokens?.accessToken;

  const [members, setMembers] = useState<Member[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadMembers = useCallback(async () => {
    if (!accessToken) return;
    setIsLoading(true);
    setError(null);
    try {
      const response = await listMembers(accessToken);
      setMembers(response.members);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar membros');
    } finally {
      setIsLoading(false);
    }
  }, [accessToken]);

  const invite = useCallback(
    async (email: string, displayName: string, role: UserRole): Promise<boolean> => {
      if (!accessToken) return false;
      try {
        await inviteMember(accessToken, { email, displayName, role });
        await loadMembers();
        return true;
      } catch (err) {
        throw err;
      }
    },
    [accessToken, loadMembers]
  );

  const toggleStatus = useCallback(
    async (member: Member): Promise<boolean> => {
      if (!accessToken) return false;
      const newStatus = member.status === 'Active' ? 'inactive' : 'active';
      try {
        await updateMemberStatus(accessToken, {
          userId: member.userId,
          status: newStatus,
        });
        await loadMembers();
        return true;
      } catch (err) {
        throw err;
      }
    },
    [accessToken, loadMembers]
  );

  const changeRole = useCallback(
    async (member: Member, role: UserRole): Promise<boolean> => {
      if (!accessToken) return false;
      try {
        await updateMemberRole(accessToken, {
          userId: member.userId,
          role,
        });
        await loadMembers();
        return true;
      } catch (err) {
        throw err;
      }
    },
    [accessToken, loadMembers]
  );

  return {
    members,
    isLoading,
    error,
    loadMembers,
    invite,
    toggleStatus,
    changeRole,
  };
}
