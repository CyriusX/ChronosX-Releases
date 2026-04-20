import { useState, useEffect, useCallback, useRef } from 'react';
import { motion, AnimatePresence } from 'motion/react';
import { ExternalLink, Check, AlertTriangle, Loader2, Crown, Users, Monitor, FileText, Download, X, Minus, Plus, ArrowLeft } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { WebSidebar } from '../components/WebSidebar';
import { usePermissions } from '../hooks/usePermissions';
import { useNotifications } from '@desktop/stores/uiStore';
import { SkeletonShimmer } from '@desktop/components/ui/SkeletonShimmer';
import {
  getSubscriptionStatus,
  listPlans,
  createCheckout,
  createPortal,
  cancelSubscription,
  listInvoices,
  updateSeats,
} from '../services/billingApi';
import type { SubscriptionStatusResponse, PlanResponse, SubscriptionStatus, InvoiceResponse } from '@desktop/types/billing';

const STATUS_COLORS: Record<SubscriptionStatus, string> = {
  none: 'text-[rgba(245,247,251,0.4)]',
  active: 'text-[#22c55e]',
  trialing: 'text-[#3b82f6]',
  past_due: 'text-[#f59e0b]',
  canceled: 'text-[#f87171]',
  unpaid: 'text-[#f87171]',
  incomplete: 'text-[rgba(245,247,251,0.4)]',
};

const STATUS_DOT_COLORS: Record<SubscriptionStatus, string> = {
  none: 'bg-[rgba(245,247,251,0.3)]',
  active: 'bg-[#22c55e]',
  trialing: 'bg-[#3b82f6]',
  past_due: 'bg-[#f59e0b]',
  canceled: 'bg-[#f87171]',
  unpaid: 'bg-[#f87171]',
  incomplete: 'bg-[rgba(245,247,251,0.3)]',
};

export default function Billing() {
  const navigate = useNavigate();
  const { t, i18n } = useTranslation();
  const locale = i18n.language;
  const { isAdmin } = usePermissions();
  const { notify } = useNotifications();
  const contentRef = useRef<HTMLDivElement>(null);

  const STATUS_LABELS: Record<SubscriptionStatus, string> = {
    none: t('billing.status.none'),
    active: t('billing.status.active'),
    trialing: t('billing.status.trialing'),
    past_due: t('billing.status.past_due'),
    canceled: t('billing.status.canceled'),
    unpaid: t('billing.status.unpaid'),
    incomplete: t('billing.status.incomplete'),
  };

  const INVOICE_STATUS_LABELS: Record<string, string> = {
    paid: t('billing.invoiceStatus.paid'),
    open: t('billing.invoiceStatus.open'),
    void: t('billing.invoiceStatus.void'),
    cancelled: t('billing.invoiceStatus.cancelled'),
    uncollectible: t('billing.invoiceStatus.uncollectible'),
    draft: t('billing.invoiceStatus.draft'),
    refunded: t('billing.invoiceStatus.refunded'),
  };

  const [subscription, setSubscription] = useState<SubscriptionStatusResponse | null>(null);
  const [plans, setPlans] = useState<PlanResponse[]>([]);
  const [invoices, setInvoices] = useState<InvoiceResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRedirecting, setIsRedirecting] = useState(false);
  const [isCancelling, setIsCancelling] = useState(false);
  const [showCancelConfirm, setShowCancelConfirm] = useState(false);
  const [seatQuantity, setSeatQuantity] = useState(1);
  const [isUpdatingSeats, setIsUpdatingSeats] = useState(false);

  const loadBilling = useCallback(async () => {
    setIsLoading(true);
    try {
      const [sub, planList, invoiceList] = await Promise.all([
        getSubscriptionStatus(),
        listPlans(),
        listInvoices(),
      ]);
      setSubscription(sub);
      setPlans(planList);
      setInvoices(invoiceList);
      if (sub?.quantity) setSeatQuantity(sub.quantity);
      return { sub, invoices: invoiceList };
    } catch (error) {
      console.error('[Billing] Error loading billing data:', error);
      return { sub: null, invoices: [] };
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    const onPageShow = (e: PageTransitionEvent) => {
      if (e.persisted) loadBilling();
    };
    window.addEventListener('pageshow', onPageShow);

    loadBilling().then(({ sub, invoices: inv }) => {
      const needsRetry = sub && sub.status !== 'none' && inv.length === 0;
      if (needsRetry) {
        const retry = setTimeout(() => loadBilling(), 3000);
        return () => clearTimeout(retry);
      }
    });

    return () => window.removeEventListener('pageshow', onPageShow);
  }, [loadBilling]);

  const handleUpgrade = async (planId: string, quantity: number = 1) => {
    if (!isAdmin) return;
    setIsRedirecting(true);
    try {
      const result = await createCheckout(planId, quantity);
      window.location.href = result.checkoutUrl;
    } catch (error) {
      console.error('[Billing] Error creating checkout:', error);
      notify.error(t('billing.checkoutError'));
      setIsRedirecting(false);
    }
  };

  const handleManage = async () => {
    if (!isAdmin) return;
    setIsRedirecting(true);
    try {
      const result = await createPortal();
      window.location.href = result.portalUrl;
    } catch (error) {
      console.error('[Billing] Error opening portal:', error);
      notify.error(t('billing.portalError'));
      setIsRedirecting(false);
    }
  };

  const handleCancel = async () => {
    if (!isAdmin) return;
    setIsCancelling(true);
    try {
      const result = await cancelSubscription();
      if (result.immediateCancel) {
        notify.success(t('billing.cancelImmediate'));
      } else {
        notify.success(t('billing.cancelScheduled'));
      }
      setShowCancelConfirm(false);
      await new Promise(r => setTimeout(r, 2000));
      await loadBilling();
    } catch (error) {
      console.error('[Billing] Error cancelling subscription:', error);
      notify.error(t('billing.cancelError'));
    } finally {
      setIsCancelling(false);
    }
  };

  const handleUpdateSeats = async () => {
    if (!subscription || seatQuantity === subscription.quantity) return;
    setIsUpdatingSeats(true);
    try {
      const result = updateSeats(seatQuantity);
      const r = await result;
      if (r.prorationType === 'full_refund') {
        notify.success(t('billing.seatsRefund', { count: seatQuantity }));
      } else if (seatQuantity < subscription.quantity) {
        notify.success(t('billing.seatsReduce', { count: seatQuantity }));
      } else {
        notify.success(t('billing.seatsIncrease', { count: seatQuantity }));
      }
      const countBefore = invoices.length;
      for (let attempt = 0; attempt < 8; attempt++) {
        await new Promise(r => setTimeout(r, 2500));
        try {
          const refreshed = await listInvoices();
          setInvoices(refreshed);
          if (refreshed.length > countBefore) break;
        } catch { /* retry */ }
      }
      await loadBilling();
    } catch (error: any) {
      const msg = error?.message || t('billing.seatsError');
      notify.error(msg);
    } finally {
      setIsUpdatingSeats(false);
    }
  };

  const status = (subscription?.status ?? 'none') as SubscriptionStatus;
  const isActive = status === 'active' || status === 'trialing';
  const isPastDue = status === 'past_due';
  const isUnpaid = status === 'unpaid';
  const hasNoPlan = status === 'none' || isUnpaid || status === 'canceled';
  const isWithinCoolingOff = isActive && !!subscription?.currentPeriodEnd &&
    (Date.now() - new Date(subscription.currentPeriodStart ?? subscription.currentPeriodEnd!).getTime()) < 7 * 24 * 60 * 60 * 1000;

  return (
    <div className="flex h-screen bg-[#0b0d14]">
      <WebSidebar />

      <main className="flex-1 overflow-auto">
        <div ref={contentRef} className="p-6 max-w-4xl">
          <motion.div
            initial={{ opacity: 0, y: 12 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3 }}
            className="flex items-center gap-4 mb-6"
          >
            <motion.button
              whileHover={{ scale: 1.08 }}
              whileTap={{ scale: 0.95 }}
              onClick={() => navigate(-1)}
              className="w-9 h-9 rounded-[10px] bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] flex items-center justify-center hover:bg-[rgba(255,255,255,0.08)] transition-colors"
            >
              <ArrowLeft className="w-4 h-4 text-[rgba(245,247,251,0.6)]" />
            </motion.button>
            <div>
              <h1 className="text-[20px] font-semibold text-[#f5f7fb]">{t('billing.webTitle')}</h1>
              <p className="text-[12px] text-[rgba(245,247,251,0.4)]">
                {t('billing.webSubtitle')}
              </p>
            </div>
          </motion.div>

          {isLoading ? (
            <div className="space-y-4">
              {Array.from({ length: 3 }).map((_, i) => (
                <SkeletonShimmer key={i} height={80} rounded="rounded-xl" />
              ))}
            </div>
          ) : (
            <>
              <SubscriptionTab
                subscription={subscription}
                plans={plans}
                status={status}
                isActive={isActive}
                isPastDue={isPastDue}
                isUnpaid={isUnpaid}
                hasNoPlan={hasNoPlan}
                isWithinCoolingOff={isWithinCoolingOff}
                isAdmin={isAdmin}
                isRedirecting={isRedirecting}
                isCancelling={isCancelling}
                showCancelConfirm={showCancelConfirm}
                seatQuantity={seatQuantity}
                isUpdatingSeats={isUpdatingSeats}
                setSeatQuantity={setSeatQuantity}
                handleUpgrade={handleUpgrade}
                handleManage={handleManage}
                handleCancel={handleCancel}
                handleUpdateSeats={handleUpdateSeats}
                setShowCancelConfirm={setShowCancelConfirm}
                statusLabels={STATUS_LABELS}
                locale={locale}
              />
              <InvoicesTab invoices={invoices} invoiceStatusLabels={INVOICE_STATUS_LABELS} locale={locale} />
            </>
          )}
        </div>
      </main>
    </div>
  );
}

// ============================================================================
// Subscription Tab
// ============================================================================

function SubscriptionTab({
  subscription, plans, status, isActive, isPastDue, isUnpaid, hasNoPlan,
  isWithinCoolingOff, isAdmin, isRedirecting, isCancelling, showCancelConfirm,
  seatQuantity, isUpdatingSeats, setSeatQuantity,
  handleUpgrade, handleManage, handleCancel, handleUpdateSeats, setShowCancelConfirm,
  statusLabels, locale,
}: {
  subscription: SubscriptionStatusResponse | null;
  plans: PlanResponse[];
  status: SubscriptionStatus;
  isActive: boolean;
  isPastDue: boolean;
  isUnpaid: boolean;
  hasNoPlan: boolean;
  isWithinCoolingOff: boolean;
  isAdmin: boolean;
  isRedirecting: boolean;
  isCancelling: boolean;
  showCancelConfirm: boolean;
  seatQuantity: number;
  isUpdatingSeats: boolean;
  setSeatQuantity: (fn: (q: number) => number) => void;
  handleUpgrade: (planId: string, qty: number) => void;
  handleManage: () => void;
  handleCancel: () => void;
  handleUpdateSeats: () => void;
  setShowCancelConfirm: (v: boolean) => void;
  statusLabels: Record<SubscriptionStatus, string>;
  locale: string;
}) {
  const { t } = useTranslation();

  return (
    <div className="space-y-8">
      {/* Status Card */}
      <div className={`bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border rounded-2xl p-6 ${
        isPastDue ? 'border-[rgba(245,158,11,0.3)]' :
        hasNoPlan ? 'border-[rgba(248,113,113,0.3)]' :
        'border-[rgba(255,255,255,0.06)]'
      }`}>
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-3">
            <div className={`w-3 h-3 rounded-full ${STATUS_DOT_COLORS[status]}`} />
            <span className={`text-[14px] font-medium ${STATUS_COLORS[status]}`}>
              {statusLabels[status]}
            </span>
          </div>
          {subscription?.planName && (
            <span className="text-[12px] text-[rgba(245,247,251,0.4)] bg-[rgba(255,255,255,0.04)] px-3 py-1 rounded-full">
              {subscription.planName}
            </span>
          )}
        </div>

        {subscription && (
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-4">
            <UsageStat icon={Users} label={t('billing.users')} current={subscription.currentUsers} max={subscription.quantity} />
            <UsageStat icon={Monitor} label={t('billing.devices')} current={subscription.currentDevices} max={subscription.quantity} />
          </div>
        )}

        {/* Seat management */}
        {isActive && isAdmin && subscription && (
          <div className="mb-4 p-4 rounded-xl bg-[rgba(255,255,255,0.02)] border border-[rgba(255,255,255,0.06)]">
            <div className="flex items-center justify-between mb-3">
              <div>
                <p className="text-[13px] font-medium text-[#f5f7fb]">{t('billing.manageSeats')}</p>
                <p className="text-[11px] text-[rgba(245,247,251,0.4)] mt-0.5">
                  {t('billing.manageSeatsDesc')}
                </p>
              </div>
              {isWithinCoolingOff && (
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-[rgba(34,197,94,0.1)] border border-[rgba(34,197,94,0.2)] text-[#22c55e]">
                  {t('billing.coolingOffBadge')}
                </span>
              )}
            </div>
            <div className="flex items-center gap-3">
              <button
                onClick={() => setSeatQuantity(q => Math.max(subscription.currentUsers || 1, q - 1))}
                disabled={seatQuantity <= (subscription.currentUsers || 1)}
                className="w-9 h-9 rounded-lg bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] flex items-center justify-center text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.1)] transition-colors disabled:opacity-30"
              >
                <Minus className="w-4 h-4" />
              </button>
              <span className="text-[18px] font-semibold text-[#f5f7fb] min-w-[2ch] text-center">{seatQuantity}</span>
              <button
                onClick={() => setSeatQuantity(q => q + 1)}
                className="w-9 h-9 rounded-lg bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] flex items-center justify-center text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.1)] transition-colors"
              >
                <Plus className="w-4 h-4" />
              </button>
              <div className="flex-1" />
              {seatQuantity !== subscription.quantity && (
                <button
                  onClick={handleUpdateSeats}
                  disabled={isUpdatingSeats}
                  className="flex items-center gap-2 px-4 py-2 rounded-xl bg-gradient-to-r from-[#3b82f6] to-[#06b6d4] text-[13px] font-medium text-white hover:opacity-90 transition-opacity disabled:opacity-50"
                >
                  {isUpdatingSeats ? <Loader2 className="w-4 h-4 animate-spin" /> : seatQuantity < subscription.quantity ? t('billing.reduceRefund') : t('billing.addSeats')}
                </button>
              )}
            </div>
          </div>
        )}

        {/* Grace period warning */}
        {isPastDue && subscription?.gracePeriodEnd && (
          <div className="flex items-start gap-3 p-3 rounded-xl bg-[rgba(245,158,11,0.08)] border border-[rgba(245,158,11,0.15)] mb-4">
            <AlertTriangle className="w-4 h-4 text-[#f59e0b] flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-[13px] text-[#f59e0b] font-medium">{t('billing.pastDueTitle')}</p>
              <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">
                {t('billing.pastDueDesc', { date: new Date(subscription.gracePeriodEnd).toLocaleDateString(locale) })}
              </p>
            </div>
          </div>
        )}

        {/* Scheduled cancellation */}
        {isActive && subscription?.cancelAtPeriodEnd && (
          <div className="flex items-start gap-3 p-3 rounded-xl bg-[rgba(245,158,11,0.08)] border border-[rgba(245,158,11,0.15)] mb-4">
            <AlertTriangle className="w-4 h-4 text-[#f59e0b] flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-[13px] text-[#f59e0b] font-medium">{t('billing.scheduledCancelTitle')}</p>
              <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">
                {t('billing.scheduledCancelDesc', {
                  date: subscription.currentPeriodEnd
                    ? new Date(subscription.currentPeriodEnd).toLocaleDateString(locale)
                    : t('billing.endOfPeriod'),
                })}
              </p>
            </div>
          </div>
        )}

        {/* No plan warning */}
        {hasNoPlan && (
          <div className="flex items-start gap-3 p-3 rounded-xl bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.15)] mb-4">
            <AlertTriangle className="w-4 h-4 text-[#f87171] flex-shrink-0 mt-0.5" />
            <div>
              <p className="text-[13px] text-[#f87171] font-medium">
                {isUnpaid ? t('billing.monitoringPaused') : t('billing.noActiveSubscription')}
              </p>
              <p className="text-[12px] text-[rgba(245,247,251,0.5)] mt-0.5">
                {isUnpaid ? t('billing.expiredDesc') : t('billing.choosePlanDesc')}
              </p>
            </div>
          </div>
        )}

        {/* Action buttons */}
        {isActive && isAdmin && subscription?.planTier && (
          <div className="flex items-center gap-3">
            <button
              onClick={handleManage}
              disabled={isRedirecting}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[rgba(255,255,255,0.04)] border border-[rgba(255,255,255,0.08)] text-[13px] font-medium text-[rgba(245,247,251,0.7)] hover:bg-[rgba(255,255,255,0.08)] hover:text-[#f5f7fb] transition-colors disabled:opacity-50"
            >
              {isRedirecting ? <Loader2 className="w-4 h-4 animate-spin" /> : <ExternalLink className="w-4 h-4" />}
              {t('billing.manageSubscription')}
            </button>
            <button
              onClick={() => setShowCancelConfirm(true)}
              className="flex items-center gap-2 px-4 py-2.5 rounded-xl bg-[rgba(248,113,113,0.06)] border border-[rgba(248,113,113,0.15)] text-[13px] font-medium text-[rgba(248,113,113,0.7)] hover:bg-[rgba(248,113,113,0.12)] hover:text-[#f87171] transition-colors"
            >
              <X className="w-4 h-4" />
              {t('billing.cancelPlan')}
            </button>
          </div>
        )}
      </div>

      {/* Cancel modal */}
      <AnimatePresence>
        {showCancelConfirm && (
          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            className="fixed inset-0 z-50 flex items-center justify-center bg-[#0b0d14]/80 backdrop-blur-sm"
            onClick={() => setShowCancelConfirm(false)}
          >
            <motion.div
              initial={{ scale: 0.95, y: 20 }}
              animate={{ scale: 1, y: 0 }}
              exit={{ scale: 0.95, y: 20 }}
              className="w-full max-w-md mx-4 bg-[#1a1d2e] border border-[rgba(255,255,255,0.08)] rounded-2xl p-6"
              onClick={(e) => e.stopPropagation()}
            >
              <h3 className="text-[18px] font-semibold text-[#f5f7fb] mb-2">{t('billing.cancelConfirmTitle')}</h3>
              <p className="text-[14px] text-[rgba(245,247,251,0.5)] mb-6">
                {isWithinCoolingOff
                  ? t('billing.cancelCoolingOff')
                  : t('billing.cancelNormal', {
                      date: subscription?.currentPeriodEnd
                        ? new Date(subscription.currentPeriodEnd).toLocaleDateString(locale)
                        : t('billing.currentPeriodEnd'),
                    })}
              </p>
              <div className="flex items-center gap-3 justify-end">
                <button onClick={() => setShowCancelConfirm(false)} className="px-4 py-2.5 rounded-xl text-[13px] font-medium text-[rgba(245,247,251,0.6)] hover:text-[#f5f7fb] transition-colors">
                  {t('billing.back')}
                </button>
                <button
                  onClick={handleCancel}
                  disabled={isCancelling}
                  className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[rgba(248,113,113,0.15)] border border-[rgba(248,113,113,0.3)] text-[13px] font-medium text-[#f87171] hover:bg-[rgba(248,113,113,0.25)] transition-colors disabled:opacity-50"
                >
                  {isCancelling ? <Loader2 className="w-4 h-4 animate-spin" /> : <X className="w-4 h-4" />}
                  {t('billing.yesCancel')}
                </button>
              </div>
            </motion.div>
          </motion.div>
        )}
      </AnimatePresence>

      {/* Plans */}
      {isAdmin && (hasNoPlan || isPastDue) && plans.length > 0 && (
        <div className="space-y-4">
          <h3 className="text-[14px] font-medium text-[rgba(245,247,251,0.7)]">{t('billing.availablePlans')}</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {plans.map((plan) => (
              <PlanCard
                key={plan.id}
                plan={plan}
                isCurrent={isActive && subscription?.planTier === plan.tier}
                currentUsers={subscription?.currentUsers ?? 1}
                onSelect={(qty) => handleUpgrade(plan.id, qty)}
                isLoading={isRedirecting}
              />
            ))}
          </div>
        </div>
      )}

      {!isAdmin && (
        <p className="text-[12px] text-[rgba(245,247,251,0.35)]">
          {t('billing.adminOnlyNotice')}
        </p>
      )}
    </div>
  );
}

// ============================================================================
// Invoices Tab
// ============================================================================

function InvoicesTab({ invoices, invoiceStatusLabels, locale }: { invoices: InvoiceResponse[]; invoiceStatusLabels: Record<string, string>; locale: string }) {
  const { t } = useTranslation();

  if (invoices.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <FileText className="w-12 h-12 text-[rgba(245,247,251,0.15)] mb-4" />
        <p className="text-[14px] text-[rgba(245,247,251,0.4)]">{t('billing.noInvoices')}</p>
        <p className="text-[12px] text-[rgba(245,247,251,0.25)] mt-1">{t('billing.noInvoicesDesc')}</p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <h3 className="text-[14px] font-medium text-[rgba(245,247,251,0.7)]">{t('billing.invoiceHistory')}</h3>
      <div className="bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)] rounded-2xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr className="border-b border-[rgba(255,255,255,0.06)]">
                <th className="text-left text-[11px] font-medium text-[rgba(245,247,251,0.35)] uppercase tracking-wider px-5 py-3">{t('billing.date')}</th>
                <th className="text-left text-[11px] font-medium text-[rgba(245,247,251,0.35)] uppercase tracking-wider px-5 py-3">{t('billing.plan')}</th>
                <th className="text-left text-[11px] font-medium text-[rgba(245,247,251,0.35)] uppercase tracking-wider px-5 py-3">{t('billing.description')}</th>
                <th className="text-right text-[11px] font-medium text-[rgba(245,247,251,0.35)] uppercase tracking-wider px-5 py-3">{t('billing.amount')}</th>
                <th className="text-center text-[11px] font-medium text-[rgba(245,247,251,0.35)] uppercase tracking-wider px-5 py-3">{t('billing.invoiceStatusLabel')}</th>
                <th className="text-right text-[11px] font-medium text-[rgba(245,247,251,0.35)] uppercase tracking-wider px-5 py-3"></th>
              </tr>
            </thead>
            <tbody>
              {invoices.map((invoice) => (
                <tr key={invoice.id} className="border-b border-[rgba(255,255,255,0.03)] last:border-b-0 hover:bg-[rgba(255,255,255,0.02)] transition-colors">
                  <td className="px-5 py-3.5">
                    <span className="text-[13px] text-[rgba(245,247,251,0.8)]">
                      {new Date(invoice.periodStart).toLocaleDateString(locale)}
                    </span>
                  </td>
                  <td className="px-5 py-3.5">
                    <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{invoice.planName || '-'}</span>
                  </td>
                  <td className="px-5 py-3.5">
                    <span className="text-[13px] text-[rgba(245,247,251,0.6)]">
                      {invoice.description || t('billing.userCount', { count: invoice.quantity })}
                    </span>
                  </td>
                  <td className="px-5 py-3.5 text-right">
                    <span className="text-[13px] font-medium text-[rgba(245,247,251,0.9)]">
                      {(invoice.amountCents / 100).toLocaleString(locale, { style: 'currency', currency: invoice.currency.toUpperCase() })}
                    </span>
                  </td>
                  <td className="px-5 py-3.5 text-center">
                    <InvoiceStatusBadge status={invoice.status} refundStatus={invoice.refundStatus} labels={invoiceStatusLabels} />
                  </td>
                  <td className="px-5 py-3.5 text-right">
                    {invoice.pdfUrl && (
                      <a
                        href={invoice.pdfUrl}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="inline-flex items-center gap-1.5 text-[12px] text-[rgba(245,247,251,0.4)] hover:text-[rgba(245,247,251,0.7)] transition-colors"
                      >
                        <Download className="w-3.5 h-3.5" />
                        PDF
                      </a>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

// ============================================================================
// Shared Sub-components
// ============================================================================

function InvoiceStatusBadge({ status, refundStatus, labels }: { status: string; refundStatus?: string | null; labels: Record<string, string> }) {
  const { t } = useTranslation();

  if (refundStatus === 'refunded') {
    return (
      <span className="inline-flex px-2.5 py-0.5 rounded-full text-[11px] font-medium border bg-[rgba(139,92,246,0.1)] text-[#a78bfa] border-[rgba(139,92,246,0.2)]">
        {t('billing.invoiceStatus.refunded')}
      </span>
    );
  }

  const styles: Record<string, string> = {
    paid: 'bg-[rgba(34,197,94,0.1)] text-[#22c55e] border-[rgba(34,197,94,0.2)]',
    open: 'bg-[rgba(245,158,11,0.1)] text-[#f59e0b] border-[rgba(245,158,11,0.2)]',
    void: 'bg-[rgba(248,113,113,0.1)] text-[#f87171] border-[rgba(248,113,113,0.2)]',
    cancelled: 'bg-[rgba(248,113,113,0.1)] text-[#f87171] border-[rgba(248,113,113,0.2)]',
    uncollectible: 'bg-[rgba(248,113,113,0.1)] text-[#f87171] border-[rgba(248,113,113,0.2)]',
    draft: 'bg-[rgba(255,255,255,0.04)] text-[rgba(245,247,251,0.4)] border-[rgba(255,255,255,0.08)]',
  };

  return (
    <span className={`inline-flex px-2.5 py-0.5 rounded-full text-[11px] font-medium border ${styles[status] || styles.draft}`}>
      {labels[status] || status}
    </span>
  );
}

function UsageStat({ icon: Icon, label, current, max }: {
  icon: typeof Users;
  label: string;
  current: number;
  max: number | null;
}) {
  const isUnlimited = max === null || max === 0;
  const isNearLimit = !isUnlimited && max !== null && current / max > 0.8;

  return (
    <div className="flex items-center gap-2.5 py-2">
      <Icon className={`w-4 h-4 ${isNearLimit ? 'text-[#f59e0b]' : 'text-[rgba(245,247,251,0.3)]'}`} />
      <div>
        <p className="text-[11px] text-[rgba(245,247,251,0.4)]">{label}</p>
        <p className="text-[13px] text-[rgba(245,247,251,0.9)]">
          {current}{isUnlimited ? '' : ` / ${max}`}
        </p>
      </div>
    </div>
  );
}

function PlanCard({ plan, isCurrent, currentUsers, onSelect, isLoading }: {
  plan: PlanResponse;
  isCurrent: boolean;
  currentUsers: number;
  onSelect: (quantity: number) => void;
  isLoading: boolean;
}) {
  const { t } = useTranslation();
  const [quantity, setQuantity] = useState(Math.max(1, currentUsers));
  const isEnterprise = plan.tier === 'enterprise';
  const hasPerSeatPricing = plan.monthlyPriceCents > 0 && !isEnterprise;
  const maxQty = plan.maxUsers > 0 ? plan.maxUsers : 999;
  const totalPriceCents = plan.monthlyPriceCents * quantity;

  return (
    <motion.div
      whileHover={{ scale: 1.01 }}
      className={`relative bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border rounded-2xl p-6 transition-colors ${
        isEnterprise ? 'border-[rgba(139,92,246,0.3)]' : 'border-[rgba(255,255,255,0.06)]'
      }`}
    >
      {isEnterprise && (
        <div className="absolute -top-2.5 right-4">
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-gradient-to-r from-[#8B5CF6] to-[#6366f1] text-[10px] font-semibold text-white">
            <Crown className="w-3 h-3" />
            {t('billing.popular')}
          </span>
        </div>
      )}

      <div className="mb-4">
        <h4 className="text-[16px] font-semibold text-[#f5f7fb]">{plan.name}</h4>
        <div className="flex items-baseline gap-1 mt-1">
          <span className="text-[24px] font-bold text-[#f5f7fb]">
            {plan.monthlyPriceCents === 0 ? t('billing.uponRequest') : `$${(plan.monthlyPriceCents / 100).toFixed(2)}`}
          </span>
          {plan.monthlyPriceCents > 0 && (
            <span className="text-[12px] text-[rgba(245,247,251,0.4)]">{t('billing.perUserMonth')}</span>
          )}
        </div>
      </div>

      <div className="space-y-2 mb-5">
        <PlanFeature enabled={true} label={plan.maxUsers === 0 ? t('billing.upToUsersUnlimited') : t('billing.upToUsers', { count: plan.maxUsers })} />
        <PlanFeature enabled={true} label={plan.maxDevices === 0 ? t('billing.upToDevicesUnlimited') : t('billing.upToDevices', { count: plan.maxDevices })} />
        {plan.features.machineMonitoring && <PlanFeature enabled={true} label={t('billing.machineMonitoring')} />}
        {plan.features.advancedReports && <PlanFeature enabled={true} label={t('billing.advancedReports')} />}
        {plan.features.focusMode && <PlanFeature enabled={true} label={t('billing.focusMode')} />}
        {plan.features.linearIntegration && <PlanFeature enabled={true} label={t('billing.linearIntegration')} />}
        {plan.features.apiAccess && <PlanFeature enabled={true} label={t('billing.apiAccess')} />}
        {plan.features.prioritySupport && <PlanFeature enabled={true} label={t('billing.prioritySupport')} />}
      </div>

      {hasPerSeatPricing && !isCurrent && (
        <div className="mb-4 p-3 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          <div className="flex items-center justify-between mb-2">
            <span className="text-[12px] text-[rgba(245,247,251,0.5)]">{t('billing.numberOfUsers')}</span>
            <span className="text-[13px] font-semibold text-[#f5f7fb]">${(totalPriceCents / 100).toFixed(2)}/mo</span>
          </div>
          <div className="flex items-center gap-3">
            <button
              onClick={() => setQuantity(q => Math.max(1, q - 1))}
              disabled={quantity <= 1}
              className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] flex items-center justify-center text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.1)] transition-colors disabled:opacity-30"
            >
              <Minus className="w-4 h-4" />
            </button>
            <span className="text-[16px] font-semibold text-[#f5f7fb] min-w-[2ch] text-center">{quantity}</span>
            <button
              onClick={() => setQuantity(q => Math.min(maxQty, q + 1))}
              disabled={quantity >= maxQty}
              className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] flex items-center justify-center text-[rgba(245,247,251,0.6)] hover:bg-[rgba(255,255,255,0.1)] transition-colors disabled:opacity-30"
            >
              <Plus className="w-4 h-4" />
            </button>
          </div>
        </div>
      )}

      {isCurrent ? (
        <div className="flex items-center justify-center gap-2 py-2.5 rounded-xl bg-[rgba(34,197,94,0.1)] border border-[rgba(34,197,94,0.2)] text-[13px] text-[#22c55e] font-medium">
          <Check className="w-4 h-4" />
          {t('billing.currentPlan')}
        </div>
      ) : (
        <button
          onClick={() => onSelect(quantity)}
          disabled={isLoading}
          className={`w-full flex items-center justify-center gap-2 py-2.5 rounded-xl text-[13px] font-medium transition-colors disabled:opacity-50 ${
            isEnterprise
              ? 'bg-gradient-to-r from-[#8B5CF6] to-[#6366f1] text-white hover:opacity-90'
              : 'bg-gradient-to-r from-[#3b82f6] to-[#06b6d4] text-white hover:opacity-90'
          }`}
        >
          {isLoading ? (
            <Loader2 className="w-4 h-4 animate-spin" />
          ) : (
            plan.monthlyPriceCents === 0
              ? t('billing.contactUs')
              : hasPerSeatPricing
                ? t('billing.subscribeUsers', { count: quantity })
                : t('billing.subscribePlan')
          )}
        </button>
      )}
    </motion.div>
  );
}

function PlanFeature({ enabled, label }: { enabled: boolean; label: string }) {
  return (
    <div className="flex items-center gap-2">
      <Check className={`w-3.5 h-3.5 ${enabled ? 'text-[#22c55e]' : 'text-[rgba(245,247,251,0.15)]'}`} />
      <span className="text-[12px] text-[rgba(245,247,251,0.6)]">{label}</span>
    </div>
  );
}
