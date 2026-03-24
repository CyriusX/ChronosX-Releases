/**
 * useFocusModePolicy Hook - Fetches and manages Focus Mode policy
 *
 * Fetches the organization's Focus Mode policy from the API
 * and stores it in the policyStore for global access.
 * Also sends the policy to the AgentService via IPC.
 *
 * CX-139: Integration with organization policy
 */

import { useCallback, useEffect } from 'react';
import { useAuthStore, selectUser } from '../stores/authStore';
import { usePolicyStore } from '../stores/policyStore';
import { useIpc } from './useIpc';
import { getOrgPolicy } from '../services/policyApi';
import type { FocusModePolicy } from '../types/settings';

export interface UseFocusModePolicyReturn {
  focusModePolicy: FocusModePolicy | null;
  isLoading: boolean;
  error: string | null;
  refresh: () => Promise<void>;
}

/**
 * Hook to fetch and manage Focus Mode policy
 */
export function useFocusModePolicy(): UseFocusModePolicyReturn {
  const user = useAuthStore(selectUser);
  const { sendCommand, isConnected } = useIpc();
  const { focusModePolicy, isLoading, error, setFocusModePolicy, setLoading, setError } = usePolicyStore();

  const fetchPolicy = useCallback(async () => {
    if (!user?.orgId) {
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const policy = await getOrgPolicy(user.orgId);
      setFocusModePolicy(policy.focusMode);

      // CX-139: Send policy to AgentService so FocusModeEngine can use it
      if (isConnected && policy.focusMode) {
        console.log('[useFocusModePolicy] Sending policy to AgentService:', policy.focusMode);
        const result = await sendCommand('applyFocusPolicy', policy.focusMode);
        console.log('[useFocusModePolicy] applyFocusPolicy result:', result);
      }
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to fetch policy';
      setError(errorMessage);
      console.error('[useFocusModePolicy] Error fetching policy:', err);
    } finally {
      setLoading(false);
    }
  }, [user?.orgId, setFocusModePolicy, setLoading, setError, sendCommand, isConnected]);

  // Fetch on mount and when dependencies change
  useEffect(() => {
    fetchPolicy();
  }, [fetchPolicy]);

  return {
    focusModePolicy,
    isLoading,
    error,
    refresh: fetchPolicy,
  };
}
