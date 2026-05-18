import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import { Sparkles, X } from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { isDesktopRuntime } from '../../lib/runtime';
import { useUpdate } from '../../hooks/useUpdate';

/**
 * Thin emerald stripe at the top of the app announcing a new version.
 * Renders only on desktop and only when the agent has reported an
 * update that the user hasn't dismissed yet.
 */
export function UpdateAvailableBanner() {
  const { t } = useTranslation();
  const { isAuthenticated } = useAuthStore();
  const desktopRuntime = isDesktopRuntime();
  const { updateInfo, hasPendingUpdate, startUpdate, dismissUpdate } = useUpdate();

  const visible = hasPendingUpdate && isAuthenticated && desktopRuntime && !!updateInfo;

  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          initial={{ height: 0, opacity: 0 }}
          animate={{ height: 'auto', opacity: 1 }}
          exit={{ height: 0, opacity: 0 }}
          transition={{ duration: 0.2 }}
          className="overflow-hidden"
        >
          <div className="flex items-center justify-between gap-3 px-4 py-2 bg-[rgba(16,185,129,0.08)] border-b border-[rgba(16,185,129,0.18)]">
            <div className="flex items-center gap-2 min-w-0">
              <Sparkles className="w-4 h-4 text-[#10B981] flex-shrink-0" />
              <span className="text-[12px] font-medium text-[#10B981] truncate">
                {t('update.banner.title', { version: updateInfo!.latestVersion })}
              </span>
            </div>
            <div className="flex items-center gap-2 flex-shrink-0">
              <button
                type="button"
                onClick={startUpdate}
                className="px-3 py-1 rounded-md bg-[#10B981] text-[#0a0c12] text-[11px] font-semibold hover:bg-[#0fbf78] transition-colors"
              >
                {t('update.banner.cta')}
              </button>
              <button
                type="button"
                onClick={dismissUpdate}
                aria-label={t('update.banner.dismiss')}
                className="p-1 rounded hover:bg-[rgba(255,255,255,0.06)] transition-colors"
              >
                <X className="w-3.5 h-3.5 text-[rgba(245,247,251,0.5)]" />
              </button>
            </div>
          </div>
        </motion.div>
      )}
    </AnimatePresence>
  );
}
