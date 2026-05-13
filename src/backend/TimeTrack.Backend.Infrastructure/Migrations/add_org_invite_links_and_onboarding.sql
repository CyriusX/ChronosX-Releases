-- Migration: AddOrgInviteLinksAndOnboarding (CX-228)
-- Creates org_invite_links table and adds onboarding_completed_at to orgs

CREATE TABLE IF NOT EXISTS org_invite_links (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES orgs(id) ON DELETE RESTRICT,
    token_hash VARCHAR(500) NOT NULL,
    role VARCHAR(20) NOT NULL DEFAULT 'Colaborador',
    created_by_user_id UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
    expires_at TIMESTAMP WITH TIME ZONE,
    max_uses INTEGER,
    use_count INTEGER NOT NULL DEFAULT 0,
    is_active BOOLEAN NOT NULL DEFAULT true,
    created_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT now()
);

CREATE UNIQUE INDEX IF NOT EXISTS ix_org_invite_links_token_hash ON org_invite_links(token_hash);
CREATE INDEX IF NOT EXISTS ix_org_invite_links_org_id ON org_invite_links(org_id);

ALTER TABLE orgs ADD COLUMN IF NOT EXISTS onboarding_completed_at TIMESTAMP WITH TIME ZONE;
