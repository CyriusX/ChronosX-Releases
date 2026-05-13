import { useTranslation } from 'react-i18next';
import { motion, AnimatePresence } from 'motion/react';
import { Sparkles, X } from 'lucide-react';
import { useAuthStore } from '../../stores/authStore';
import { isDesktopRuntime } from '../../lib/runtime';
import { useUpdate } from '../../hooks/useUpdate';
import { SPRING } from '../../lib/animation';

/**
 * Persistent emerald card in the bottom-right announcing a new version.
 * Stays visible until the user installs or dismisses. Coexists with the
 * generic Toaster (z-50) by sitting one z-index lower so transient
 * notifications float over it briefly if both are on screen.
 */
export function UpdateAvailableToast() {
  const { t } = useTranslation();
  const { isAuthenticated } = useAuthStore();
  const desktopRuntime = isDesktopRuntime();
  const { updateInfo, hasPendingUpdate, startUpdate, dismissUpdate } = useUpdate();

  const visible = hasPendingUpdate && isAuthenticated && desktopRuntime && !!updateInfo;

  return (
    <div className="fixed bottom-4 right-4 z-40 pointer-events-none">
      <AnimatePresence>
        {visible && (
          <motion.div
            initial={{ opacity: 0, x: 100, scale: 0.95 }}
            animate={{ opacity: 1, x: 0, scale: 1 }}
            exit={{ opacity: 0, x: 50, scale: 0.95 }}
            transition={{
              type: 'spring',
              stiffness: SPRING.snappy.stiffness,
              damping: SPRING.snappy.damping,
            }}
            className="pointer-events-auto max-w-sm w-[320px] rounded-lg border border-[rgba(16,185,129,0.4)] bg-[rgba(16,185,129,0.1)] backdrop-blur-sm shadow-lg"
          >
            <div className="flex items-start gap-3 px-4 py-3">
              <Sparkles className="w-4 h-4 text-[#10B981] flex-shrink-0 mt-0.5" />
              <div className="flex-1 min-w-0">
                <p className="text-[13px] font-semibold text-[#10B981]">
                  {t('update.toast.title', { version: updateInfo!.latestVersion })}
                </p>
                <p className="text-[11px] text-[rgba(245,247,251,0.7)] mt-1 leading-snug">
                  {t('update.toast.description')}
                </p>
                <div className="flex items-center gap-2 mt-3">
                  <button
                    type="button"
                    onClick={startUpdate}
                    className="px-3 py-1 rounded-md bg-[#10B981] text-[#0a0c12] text-[11px] font-semibold hover:bg-[#0fbf78] transition-colors"
                  >
                    {t('update.toast.cta')}
                  </button>
                  <button
                    type="button"
                    onClick={dismissUpdate}
                    className="px-2 py-1 text-[11px] text-[rgba(245,247,251,0.6)] hover:text-[rgba(245,247,251,0.9)] transition-colors"
                  >
                    {t('update.toast.later')}
                  </button>
                </div>
              </div>
              <button
                type="button"
                onClick={dismissUpdate}
                aria-label={t('update.banner.dismiss')}
                className="flex-shrink-0 opacity-60 hover:opacity-100 transition-opacity"
              >
                <X className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
              </button>
            </div>
          </motion.div>
        )}
      </AnimatePresence>
    </div>
  );
}
