import { api } from './apiClient';
import type {
  PlanResponse,
  SubscriptionStatusResponse,
  CheckoutSessionResponse,
  CustomerPortalResponse,
  InvoiceResponse,
} from '@desktop/types/billing';

export async function listPlans(): Promise<PlanResponse[]> {
  return api.get<PlanResponse[]>('/billing/plans');
}

export async function getSubscriptionStatus(): Promise<SubscriptionStatusResponse> {
  return api.get<SubscriptionStatusResponse>('/billing/subscription');
}

export async function createCheckout(planId: string, quantity: number = 1): Promise<CheckoutSessionResponse> {
  const origin = window.location.origin;
  return api.post<CheckoutSessionResponse>('/billing/checkout', {
    planId,
    quantity,
    successUrl: `${origin}/billing?billing=success`,
    cancelUrl: `${origin}/billing?billing=canceled`,
  });
}

export async function createPortal(): Promise<CustomerPortalResponse> {
  const origin = window.location.origin;
  return api.post<CustomerPortalResponse>('/billing/portal', {
    returnUrl: `${origin}/billing`,
  });
}

export async function cancelSubscription(): Promise<{ immediateCancel: boolean; effectiveDate: string | null; message: string }> {
  return api.post('/billing/cancel');
}

export async function listInvoices(limit: number = 50): Promise<InvoiceResponse[]> {
  return api.get<InvoiceResponse[]>(`/billing/invoices?limit=${limit}`);
}

export async function updateSeats(newQuantity: number): Promise<{ quantity: number; prorationType: string }> {
  return api.put('/billing/seats', { newQuantity });
}
