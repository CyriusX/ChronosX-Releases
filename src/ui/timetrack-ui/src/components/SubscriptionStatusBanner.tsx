import { useEffect, useState, useCallback } from 'react';
import { AlertTriangle, X } from 'lucide-react';
import { motion, AnimatePresence } from 'motion/react';
import { useAuthStore } from '../stores/authStore';
import { getSubscriptionStatus } from '../services/billingApi';
import type { SubscriptionStatusResponse } from '../types/billing';

/**
 * Global banner that warns about subscription issues.
 * - past_due: yellow warning with grace period countdown
 * - unpaid: red error, monitoring paused
 * - none: red, no active subscription
 *
 * Dismissible per session (stored in memory, not persisted).
 */
export function SubscriptionStatusBanner() {
  const user = useAuthStore((s) => s.user);
  const [subscription, setSubscription] = useState<SubscriptionStatusResponse | null>(null);
  const [dismissed, setDismissed] = useState(false);

  const checkSubscription = useCallback(async () => {
    try {
      const sub = await getSubscriptionStatus();
      setSubscription(sub);
    } catch {
      // Silently ignore — API might be unreachable
    }
  }, []);

  useEffect(() => {
    if (!user) return;
    checkSubscription();

    // Re-check every 5 minutes
    const interval = setInterval(checkSubscription, 5 * 60 * 1000);
    return () => clearInterval(interval);
  }, [user, checkSubscription]);

  if (!user || !subscription || dismissed) return null;

  const status = subscription.status;
  // "none" = legacy org (never subscribed), active/trialing = healthy — no banner needed
  if (status === 'active' || status === 'trialing' || status === 'none') return null;

  const isPastDue = status === 'past_due';
  const isUnpaid = status === 'unpaid' || status === 'canceled';

  return (
    <AnimatePresence>
      <motion.div
        initial={{ height: 0, opacity: 0 }}
        animate={{ height: 'auto', opacity: 1 }}
        exit={{ height: 0, opacity: 0 }}
        transition={{ duration: 0.2 }}
        className="overflow-hidden"
      >
        <div className={`flex items-center justify-between px-4 py-2.5 ${
          isPastDue
            ? 'bg-[rgba(245,158,11,0.08)] border-b border-[rgba(245,158,11,0.15)]'
            : 'bg-[rgba(248,113,113,0.08)] border-b border-[rgba(248,113,113,0.15)]'
        }`}>
          <div className="flex items-center gap-2">
            <AlertTriangle className={`w-4 h-4 ${isPastDue ? 'text-[#f59e0b]' : 'text-[#f87171]'}`} />
            <span className={`text-[12px] font-medium ${isPastDue ? 'text-[#f59e0b]' : 'text-[#f87171]'}`}>
              {isPastDue && subscription.gracePeriodEnd
                ? `Pagamento pendente. Sincronização continua até ${new Date(subscription.gracePeriodEnd).toLocaleDateString('pt-BR')}.`
                : isUnpaid
                ? 'Monitoramento pausado — assinatura inativa. Dados preservados localmente.'
                : 'Problema com a assinatura. Verifique as configurações de billing.'}
            </span>
          </div>
          <button
            onClick={() => setDismissed(true)}
            className="ml-2 p-1 rounded hover:bg-[rgba(255,255,255,0.06)] transition-colors"
          >
            <X className="w-3.5 h-3.5 text-[rgba(245,247,251,0.4)]" />
          </button>
        </div>
      </motion.div>
    </AnimatePresence>
  );
}
