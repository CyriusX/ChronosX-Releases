/**
 * Policy Store - Zustand state management for organization policies
 *
 * SOLID:
 * - SRP: Only manages policy-related state
 * - OCP: Extensible via new state slices
 * - DIP: Uses subscribeWithSelector for fine-grained subscriptions
 */

import { create } from 'zustand';
import { subscribeWithSelector } from 'zustand/middleware';
import type { FocusModePolicy } from '../types/settings';

// ============================================================================
// STATE INTERFACE
// ============================================================================

interface PolicyState {
  // Focus mode policy
  focusModePolicy: FocusModePolicy | null;

  // Loading state
  isLoading: boolean;
  error: string | null;

  // Actions
  setFocusModePolicy: (policy: FocusModePolicy | null) => void;
  setLoading: (loading: boolean) => void;
  setError: (error: string | null) => void;

  // Reset
  reset: () => void;
}

// ============================================================================
// INITIAL STATE
// ============================================================================

const initialState = {
  focusModePolicy: null as FocusModePolicy | null,
  isLoading: false,
  error: null as string | null,
};

// ============================================================================
// STORE
// ============================================================================

export const usePolicyStore = create<PolicyState>()(
  subscribeWithSelector((set) => ({
    ...initialState,

    setFocusModePolicy: (policy) => set({ focusModePolicy: policy }),

    setLoading: (isLoading) => set({ isLoading }),

    setError: (error) => set({ error }),

    reset: () => set(initialState),
  }))
);

// ============================================================================
// SELECTORS
// ============================================================================

export const selectFocusModePolicy = (state: PolicyState) => state.focusModePolicy;
export const selectIsPolicyLoading = (state: PolicyState) => state.isLoading;
export const selectPolicyError = (state: PolicyState) => state.error;

/**
 * Selector to check if focus mode is enabled
 */
export const selectIsFocusModeEnabled = (state: PolicyState) =>
  state.focusModePolicy?.enabled === true && state.focusModePolicy?.mode !== 'none';
