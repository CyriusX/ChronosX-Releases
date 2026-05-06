-- Migration: AddBillingInvoices
-- Creates billing_invoices table for invoice history tracking

CREATE TABLE IF NOT EXISTS billing_invoices (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL,
    stripe_invoice_id VARCHAR(200) NOT NULL,
    amount_cents BIGINT NOT NULL,
    currency VARCHAR(10) NOT NULL DEFAULT 'usd',
    status VARCHAR(30) NOT NULL,
    description TEXT,
    plan_name VARCHAR(100),
    quantity INT NOT NULL DEFAULT 1,
    period_start TIMESTAMPTZ NOT NULL,
    period_end TIMESTAMPTZ NOT NULL,
    pdf_url VARCHAR(500),
    paid_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_billing_invoices_stripe_invoice_id ON billing_invoices (stripe_invoice_id);
CREATE INDEX IF NOT EXISTS ix_billing_invoices_org_period ON billing_invoices (org_id, period_start);
