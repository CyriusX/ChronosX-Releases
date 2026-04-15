import { create } from 'zustand';
import type { HealthAlertItem } from '../services/maintenanceApi';

interface HealthAlertState {
  alerts: HealthAlertItem[];
  setAlerts: (alerts: HealthAlertItem[]) => void;
}

export const useHealthAlertStore = create<HealthAlertState>((set) => ({
  alerts: [],
  setAlerts: (alerts) => set({ alerts }),
}));
