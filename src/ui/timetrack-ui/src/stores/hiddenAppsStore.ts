/**
 * Hidden Apps Store — manages which apps are hidden from the activity timeline.
 * Persisted to localStorage so the preference survives across sessions.
 */

import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface HiddenAppsState {
  /** Set of app process names (lowercase) hidden from timeline */
  hiddenApps: string[];

  /** Add an app to the hidden list */
  hideApp: (processName: string) => void;

  /** Remove an app from the hidden list */
  showApp: (processName: string) => void;

  /** Check if an app is hidden */
  isHidden: (processName: string) => boolean;
}

export const useHiddenAppsStore = create<HiddenAppsState>()(
  persist(
    (set, get) => ({
      hiddenApps: [
        // Default hidden: internal tracking apps
        'timetrack.desktophost',
        'microsoft edge webview2',
      ],

      hideApp: (processName: string) => {
        const normalized = processName.toLowerCase();
        const current = get().hiddenApps;
        if (!current.includes(normalized)) {
          set({ hiddenApps: [...current, normalized] });
        }
      },

      showApp: (processName: string) => {
        const normalized = processName.toLowerCase();
        set({ hiddenApps: get().hiddenApps.filter(a => a !== normalized) });
      },

      isHidden: (processName: string) => {
        return get().hiddenApps.includes(processName.toLowerCase());
      },
    }),
    {
      name: 'timetrack-hidden-apps',
    }
  )
);
