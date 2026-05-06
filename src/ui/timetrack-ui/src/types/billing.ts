/**
 * Billing & Subscription Types
 */

export type SubscriptionStatus =
  | 'none'
  | 'active'
  | 'trialing'
  | 'past_due'
  | 'canceled'
  | 'unpaid'
  | 'incomplete';

export type PlanTier = 'pro' | 'enterprise';

export type PlanFeatureSet = Record<string, boolean>;

export interface PlanResponse {
  id: string;
  name: string;
  tier: PlanTier;
  monthlyPriceCents: number;
  yearlyPriceCents: number | null;
  maxUsers: number;
  maxDevices: number;
  stripePriceId: string;
  features: PlanFeatureSet;
}

export interface SubscriptionStatusResponse {
  status: SubscriptionStatus;
  planTier: PlanTier | null;
  planName: string | null;
  currentPeriodStart: string | null;
  currentPeriodEnd: string | null;
  trialEnd: string | null;
  gracePeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  quantity: number;
  features: PlanFeatureSet | null;
  currentUsers: number;
  currentDevices: number;
  canceledAt: string | null;
}

export interface CheckoutSessionResponse {
  checkoutUrl: string;
  sessionId: string;
}

export interface CustomerPortalResponse {
  portalUrl: string;
}

export interface InvoiceResponse {
  id: string;
  amountCents: number;
  currency: string;
  status: string;
  description: string | null;
  planName: string | null;
  quantity: number;
  periodStart: string;
  periodEnd: string;
  pdfUrl: string | null;
  paidAt: string | null;
  createdAt: string;
  refundStatus: string | null;
}
