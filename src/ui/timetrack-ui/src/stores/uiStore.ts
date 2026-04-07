import { create } from 'zustand';
import { persist } from 'zustand/middleware';

type Theme = 'dark' | 'light' | 'system';

interface UiState {
  // Theme
  theme: Theme;
  setTheme: (theme: Theme) => void;

  // Notifications
  notifications: Notification[];
  addNotification: (notification: Omit<Notification, 'id'>) => void;
  removeNotification: (id: string) => void;
  clearNotifications: () => void;

  // Modal states
  activeModal: string | null;
  openModal: (modalId: string) => void;
  closeModal: () => void;
}

interface Notification {
  id: string;
  type: 'info' | 'success' | 'warning' | 'error';
  title: string;
  message?: string;
  duration?: number;
}

export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      theme: 'dark',
      setTheme: (theme: Theme) => set({ theme }),

      notifications: [],
      addNotification: (notification: Omit<Notification, 'id'>) => {
        const id = `notification_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
        set((state: UiState) => ({
          notifications: [
            ...state.notifications,
            { ...notification, id },
          ],
        }));

        // Auto-remove after duration (default 5 seconds)
        const duration = notification.duration ?? 5000;
        if (duration > 0) {
          setTimeout(() => {
            set((state: UiState) => ({
              notifications: state.notifications.filter((n: Notification) => n.id !== id),
            }));
          }, duration);
        }
      },
      removeNotification: (id: string) =>
        set((state: UiState) => ({
          notifications: state.notifications.filter((n: Notification) => n.id !== id),
        })),
      clearNotifications: () => set({ notifications: [] }),

      activeModal: null,
      openModal: (modalId: string) => set({ activeModal: modalId }),
      closeModal: () => set({ activeModal: null }),
    }),
    {
      name: 'timetrack-ui-storage',
      partialize: (state: UiState) => ({ theme: state.theme }),
    }
  )
);

// Helper hook for notifications
export function useNotifications() {
  const { notifications, addNotification, removeNotification, clearNotifications } = useUiStore();

  const notify = {
    info: (title: string, message?: string) =>
      addNotification({ type: 'info', title, message }),
    success: (title: string, message?: string) =>
      addNotification({ type: 'success', title, message }),
    warning: (title: string, message?: string) =>
      addNotification({ type: 'warning', title, message }),
    error: (title: string, message?: string) =>
      addNotification({ type: 'error', title, message }),
  };

  return { notifications, notify, removeNotification, clearNotifications };
}
