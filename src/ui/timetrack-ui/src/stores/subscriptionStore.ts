import { create } from 'zustand';
import { getSubscriptionStatus } from '../services/billingApi';
import type { PlanFeatureSet } from '../types/billing';

interface SubscriptionState {
  features: PlanFeatureSet | null;
  status: string | null;
  isLoading: boolean;
  fetchSubscription: () => Promise<void>;
  clearSubscription: () => void;
}

export const useSubscriptionStore = create<SubscriptionState>()((set, get) => ({
  features: null,
  status: null,
  isLoading: false,

  fetchSubscription: async () => {
    if (get().features) return;
    set({ isLoading: true });
    try {
      const response = await getSubscriptionStatus();
      set({ features: response.features, status: response.status, isLoading: false });
    } catch {
      set({ isLoading: false });
    }
  },

  clearSubscription: () => set({ features: null, status: null, isLoading: false }),
}));
