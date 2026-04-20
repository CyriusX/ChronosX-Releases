/**
 * Billing API - HTTP client for subscription and billing management
 */

import { api } from './apiClient';
import type {
  PlanResponse,
  SubscriptionStatusResponse,
  CheckoutSessionResponse,
  CustomerPortalResponse,
  InvoiceResponse,
} from '../types/billing';

/** Ensure origin uses HTTPS (Stripe live mode requirement) */
function httpsOrigin(): string {
  const { origin } = window.location;
  return origin.replace(/^http:/, 'https:');
}

/**
 * List available subscription plans
 */
export async function listPlans(): Promise<PlanResponse[]> {
  return api.get<PlanResponse[]>('/billing/plans');
}

/**
 * Get current organization subscription status
 */
export async function getSubscriptionStatus(): Promise<SubscriptionStatusResponse> {
  return api.get<SubscriptionStatusResponse>('/billing/subscription');
}

/**
 * Create a Stripe Checkout session for plan subscription
 */
export async function createCheckout(planId: string, quantity: number = 1): Promise<CheckoutSessionResponse> {
  const origin = httpsOrigin();
  return api.post<CheckoutSessionResponse>('/billing/checkout', {
    planId,
    quantity,
    successUrl: `${origin}/settings?billing=success`,
    cancelUrl: `${origin}/settings?billing=canceled`,
  });
}

/**
 * Create a Stripe Customer Portal session for managing billing
 */
export async function createPortal(): Promise<CustomerPortalResponse> {
  const origin = httpsOrigin();
  return api.post<CustomerPortalResponse>('/billing/portal', {
    returnUrl: `${origin}/settings`,
  });
}

/**
 * Cancel the current subscription
 */
export async function cancelSubscription(): Promise<{ immediateCancel: boolean; effectiveDate: string | null; message: string }> {
  return api.post('/billing/cancel');
}

/**
 * List billing invoices
 */
export async function listInvoices(limit: number = 50): Promise<InvoiceResponse[]> {
  return api.get<InvoiceResponse[]>(`/billing/invoices?limit=${limit}`);
}

/**
 * Update subscription seats (quantity)
 */
export async function updateSeats(newQuantity: number): Promise<{ quantity: number; prorationType: string }> {
  return api.put('/billing/seats', { newQuantity });
}
