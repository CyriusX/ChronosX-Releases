import { useState, useEffect, useCallback } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { CreditCard, Check, AlertTriangle, Crown, Clock, Loader2, Shield } from 'lucide-react';
import { motion } from 'motion/react';
import { useAuthStore } from '../stores/authStore';
import { usePermissions } from '../hooks/usePermissions';
import { useNotifications } from '../stores/uiStore';
import { getSubscriptionStatus, listPlans, createCheckout, createPortal } from '../services/billingApi';
import type { SubscriptionStatusResponse, PlanResponse, SubscriptionStatus } from '../types/billing';

const BLOCKED_STATUSES: Set<string> = new Set(['none', 'past_due', 'unpaid', 'canceled', 'incomplete']);

export function PaywallOverlay({ children }: { children: React.ReactNode }) {
  const user = useAuthStore((s) => s.user);
  const { isAdmin } = usePermissions();
  const { notify } = useNotifications();
  const navigate = useNavigate();
  const location = useLocation();

  const [subscription, setSubscription] = useState<SubscriptionStatusResponse | null>(null);
  const [plans, setPlans] = useState<PlanResponse[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [isRedirecting, setIsRedirecting] = useState<string | null>(null);

  const loadBilling = useCallback(async () => {
    try {
      const [sub, planList] = await Promise.all([
        getSubscriptionStatus(),
        listPlans(),
      ]);
      setSubscription(sub);
      setPlans(planList);
    } catch {
      // API unreachable — don't block
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!user) return;
    loadBilling();

    const interval = setInterval(loadBilling, 5 * 60 * 1000);
    return () => clearInterval(interval);
  }, [user, loadBilling]);

  if (!user || isLoading || !subscription) return <>{children}</>;

  const status = subscription.status as SubscriptionStatus;
  const shouldBlock = BLOCKED_STATUSES.has(status);
  if (!shouldBlock) return <>{children}</>;

  // Allow Settings and Billing pages (user can manage subscription)
  const isAllowedPath = location.pathname === '/settings' || location.pathname === '/billing';
  if (isAllowedPath) return <>{children}</>;

  const isPastDue = status === 'past_due';
  const isUnpaid = status === 'unpaid';
  const graceDaysLeft = getGraceDaysLeft(subscription.gracePeriodEnd);

  const handleUpgrade = async (planId: string, quantity: number = 1) => {
    if (!isAdmin) return;
    setIsRedirecting(planId);
    try {
      const result = await createCheckout(planId, quantity);
      window.location.href = result.checkoutUrl;
    } catch {
      notify.error('Erro ao iniciar checkout. Tente novamente.');
      setIsRedirecting(null);
    }
  };

  const handleManage = async () => {
    setIsRedirecting('portal');
    try {
      const result = await createPortal();
      window.location.href = result.portalUrl;
    } catch {
      notify.error('Erro ao abrir portal.');
      setIsRedirecting(null);
    }
  };

  return (
    <>
      {children}
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-[#0b0d14]/95 backdrop-blur-sm">
        <motion.div
          initial={{ opacity: 0, scale: 0.95, y: 20 }}
          animate={{ opacity: 1, scale: 1, y: 0 }}
          transition={{ duration: 0.3 }}
          className="w-full max-w-3xl mx-4 max-h-[90vh] overflow-y-auto"
        >
          {/* Header */}
          <div className="text-center mb-8">
            <div className="w-16 h-16 mx-auto mb-4 rounded-2xl bg-gradient-to-br from-[#8B5CF6] to-[#6366f1] flex items-center justify-center">
              <CreditCard className="w-8 h-8 text-white" />
            </div>
            <h1 className="text-[24px] font-bold text-[#f5f7fb] mb-2">
              {isUnpaid ? 'Monitoramento pausado' : 'Escolha seu plano'}
            </h1>
            <p className="text-[14px] text-[rgba(245,247,251,0.5)] max-w-md mx-auto">
              {isUnpaid
                ? 'Sua assinatura expirou. Ative para retomar o monitoramento.'
                : isPastDue
                ? 'Pagamento pendente. Atualize para manter o acesso.'
                : 'Selecione um plano para começar a usar o ChronosX.'}
            </p>
          </div>

          {/* Grace period countdown */}
          {isPastDue && graceDaysLeft !== null && (
            <div className="flex items-center justify-center gap-2 mb-6 px-4 py-3 mx-auto max-w-md rounded-xl bg-[rgba(245,158,11,0.08)] border border-[rgba(245,158,11,0.15)]">
              <Clock className="w-4 h-4 text-[#f59e0b]" />
              <span className="text-[13px] text-[#f59e0b] font-medium">
                Monitoramento ativo por mais {graceDaysLeft} dia{graceDaysLeft !== 1 ? 's' : ''}. Regularize para evitar interrupção.
              </span>
            </div>
          )}

          {/* Unpaid warning */}
          {isUnpaid && (
            <div className="flex items-center justify-center gap-2 mb-6 px-4 py-3 mx-auto max-w-md rounded-xl bg-[rgba(248,113,113,0.08)] border border-[rgba(248,113,113,0.15)]">
              <AlertTriangle className="w-4 h-4 text-[#f87171]" />
              <span className="text-[13px] text-[#f87171] font-medium">
                Monitoramento pausado. Dados preservados localmente.
              </span>
            </div>
          )}

          {/* Non-admin: informational message only */}
          {!isAdmin ? (
            <div className="text-center mb-6">
              <div className="max-w-md mx-auto p-6 rounded-2xl bg-gradient-to-br from-[rgba(26,29,46,0.8)] to-[rgba(17,19,28,0.8)] border border-[rgba(255,255,255,0.06)]">
                <Shield className="w-8 h-8 text-[rgba(245,247,251,0.3)] mx-auto mb-3" />
                <p className="text-[14px] text-[rgba(245,247,251,0.6)] mb-1">
                  A organização está com o pagamento pendente.
                </p>
                <p className="text-[13px] text-[rgba(245,247,251,0.35)]">
                  Contate o administrador da organização para regularizar a assinatura.
                </p>
              </div>
            </div>
          ) : (
            <>
              {/* Manage billing for active but past_due */}
              {isPastDue && (
                <div className="flex justify-center mb-6">
                  <button
                    onClick={handleManage}
                    disabled={isRedirecting === 'portal'}
                    className="flex items-center gap-2 px-5 py-2.5 rounded-xl bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] text-[13px] font-medium text-[rgba(245,247,251,0.8)] hover:bg-[rgba(255,255,255,0.1)] transition-colors disabled:opacity-50"
                  >
                    {isRedirecting === 'portal' ? <Loader2 className="w-4 h-4 animate-spin" /> : <CreditCard className="w-4 h-4" />}
                    Atualizar pagamento
                  </button>
                </div>
              )}

              {/* Plan cards — admin only */}
              {plans.length > 0 && (
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
                  {plans.map((plan) => (
                    <PaywallPlanCard
                      key={plan.id}
                      plan={plan}
                      onSelect={(qty) => handleUpgrade(plan.id, qty)}
                      isLoading={isRedirecting === plan.id}
                    />
                  ))}
                </div>
              )}
            </>
          )}

          {/* Settings link */}
          <div className="text-center">
            <button
              onClick={() => navigate('/settings')}
              className="text-[12px] text-[rgba(245,247,251,0.3)] hover:text-[rgba(245,247,251,0.6)] transition-colors underline"
            >
              Configurações
            </button>
          </div>
        </motion.div>
      </div>
    </>
  );
}

// ============================================================================
// Helpers
// ============================================================================

function getGraceDaysLeft(gracePeriodEnd: string | null | undefined): number | null {
  if (!gracePeriodEnd) return null;
  const end = new Date(gracePeriodEnd);
  const now = new Date();
  const diffMs = end.getTime() - now.getTime();
  if (diffMs <= 0) return 0;
  return Math.ceil(diffMs / (1000 * 60 * 60 * 24));
}

// ============================================================================
// Sub-components
// ============================================================================

function PaywallPlanCard({ plan, onSelect, isLoading }: {
  plan: PlanResponse;
  onSelect: (quantity: number) => void;
  isLoading: boolean;
}) {
  const [quantity, setQuantity] = useState(1);
  const price = plan.monthlyPriceCents / 100;
  const totalPrice = price * quantity;
  const isEnterprise = plan.tier === 'enterprise';

  const increment = () => setQuantity(q => q + 1);
  const decrement = () => setQuantity(q => Math.max(1, q - 1));

  return (
    <div className={`relative bg-gradient-to-br from-[rgba(26,29,46,0.9)] to-[rgba(17,19,28,0.9)] border rounded-2xl p-6 ${
      isEnterprise ? 'border-[rgba(139,92,246,0.3)]' : 'border-[rgba(255,255,255,0.06)]'
    }`}>
      {isEnterprise && (
        <div className="absolute -top-2.5 right-4">
          <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full bg-gradient-to-r from-[#8B5CF6] to-[#6366f1] text-[10px] font-semibold text-white">
            <Crown className="w-3 h-3" />
            Popular
          </span>
        </div>
      )}

      <h3 className="text-[18px] font-semibold text-[#f5f7fb] mb-1">{plan.name}</h3>
      <div className="flex items-baseline gap-1 mb-4">
        <span className="text-[28px] font-bold text-[#f5f7fb]">
          {plan.monthlyPriceCents === 0 ? 'Sob consulta' : `$${price}`}
        </span>
        {plan.monthlyPriceCents > 0 && (
          <span className="text-[13px] text-[rgba(245,247,251,0.4)]">/user/mês</span>
        )}
      </div>

      {/* Seat selector */}
      {plan.monthlyPriceCents > 0 && (
        <div className="flex items-center gap-3 mb-4 p-3 rounded-xl bg-[rgba(255,255,255,0.03)] border border-[rgba(255,255,255,0.06)]">
          <span className="text-[12px] text-[rgba(245,247,251,0.5)]">Usuários:</span>
          <button
            onClick={decrement}
            disabled={quantity <= 1}
            className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] text-[#f5f7fb] flex items-center justify-center hover:bg-[rgba(255,255,255,0.1)] disabled:opacity-30 transition-colors"
          >
            −
          </button>
          <span className="text-[16px] font-semibold text-[#f5f7fb] min-w-[2ch] text-center">{quantity}</span>
          <button
            onClick={increment}
            className="w-8 h-8 rounded-lg bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] text-[#f5f7fb] flex items-center justify-center hover:bg-[rgba(255,255,255,0.1)] transition-colors"
          >
            +
          </button>
          <span className="text-[14px] font-semibold text-[#f5f7fb] ml-auto">
            ${totalPrice.toFixed(2)}/mês
          </span>
        </div>
      )}

      <div className="space-y-2 mb-5">
        <PlanFeature label={plan.maxUsers === 0 ? 'Usuários ilimitados' : `Até ${plan.maxUsers} usuários`} />
        <PlanFeature label={plan.maxDevices === 0 ? 'Dispositivos ilimitados' : `Até ${plan.maxDevices} dispositivos`} />
        {plan.features.machineMonitoring && <PlanFeature label="Monitoramento de máquinas" />}
        {plan.features.advancedReports && <PlanFeature label="Relatórios avançados" />}
        {plan.features.focusMode && <PlanFeature label="Modo foco" />}
        {plan.features.prioritySupport && <PlanFeature label="Suporte prioritário" />}
      </div>

      <button
        onClick={() => onSelect(quantity)}
        disabled={isLoading}
        className={`w-full flex items-center justify-center gap-2 py-3 rounded-xl text-[14px] font-semibold transition-colors disabled:opacity-50 ${
          isEnterprise
            ? 'bg-gradient-to-r from-[#8B5CF6] to-[#6366f1] text-white hover:opacity-90'
            : 'bg-[rgba(255,255,255,0.06)] border border-[rgba(255,255,255,0.1)] text-[#f5f7fb] hover:bg-[rgba(255,255,255,0.1)]'
        }`}
      >
        {isLoading ? (
          <Loader2 className="w-5 h-5 animate-spin" />
        ) : plan.monthlyPriceCents === 0 ? 'Entrar em contato' : 'Assinar agora'}
      </button>
    </div>
  );
}

function PlanFeature({ label }: { label: string }) {
  return (
    <div className="flex items-center gap-2">
      <Check className="w-4 h-4 text-[#22c55e]" />
      <span className="text-[13px] text-[rgba(245,247,251,0.6)]">{label}</span>
    </div>
  );
}
