/**
 * useMembers — Web version that imports from web memberApi
 */

import { useState, useCallback } from 'react';
import {
  listMembers,
  inviteMember,
  updateMemberStatus,
  updateMemberRole,
} from '../services/memberApi';
import type { Member, UserRole } from '@desktop/types/member';

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
  const [members, setMembers] = useState<Member[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadMembers = useCallback(async () => {
    setIsLoading(true);
    setError(null);
    try {
      const response = await listMembers();
      setMembers(response.members);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Erro ao carregar membros');
    } finally {
      setIsLoading(false);
    }
  }, []);

  const invite = useCallback(
    async (email: string, displayName: string, role: UserRole): Promise<boolean> => {
      try {
        await inviteMember({ email, displayName, role });
        await loadMembers();
        return true;
      } catch (err) {
        throw err;
      }
    },
    [loadMembers]
  );

  const toggleStatus = useCallback(
    async (member: Member): Promise<boolean> => {
      const newStatus = member.status === 'Active' ? 'inactive' : 'active';
      try {
        await updateMemberStatus({ userId: member.userId, status: newStatus });
        await loadMembers();
        return true;
      } catch (err) {
        throw err;
      }
    },
    [loadMembers]
  );

  const changeRole = useCallback(
    async (member: Member, role: UserRole): Promise<boolean> => {
      try {
        await updateMemberRole({ userId: member.userId, role });
        await loadMembers();
        return true;
      } catch (err) {
        throw err;
      }
    },
    [loadMembers]
  );

  return { members, isLoading, error, loadMembers, invite, toggleStatus, changeRole };
}
