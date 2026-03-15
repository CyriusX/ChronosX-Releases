-- Migration: AddDailyFocusScoresTable
-- Creates the daily_focus_scores table for storing daily focus score calculations
-- CX-133: Backend: Tabela focus_sessions + Cálculo do Focus Score

-- Note: Named 'daily_focus_scores' to avoid confusion with 'focus_sessions'
-- which stores individual Pomodoro/Ultradian focus mode sessions

CREATE TABLE IF NOT EXISTS daily_focus_scores (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    org_id UUID NOT NULL REFERENCES organizations(id) ON DELETE CASCADE,
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
    date DATE NOT NULL,

    -- Time metrics (milliseconds)
    total_tracked_ms BIGINT NOT NULL DEFAULT 0,
    focus_time_ms BIGINT NOT NULL DEFAULT 0,
    distraction_ms BIGINT NOT NULL DEFAULT 0,

    -- Behavior metrics
    distraction_count INT NOT NULL DEFAULT 0,
    pause_count INT NOT NULL DEFAULT 0,
    idle_count INT NOT NULL DEFAULT 0,

    -- Calculated score (0-100)
    focus_score SMALLINT NOT NULL CHECK (focus_score BETWEEN 0 AND 100),

    -- Timestamps
    calculated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- One score per user per day per organization
    CONSTRAINT uq_daily_focus_scores_org_user_date UNIQUE (org_id, user_id, date)
);

-- Index for user queries by date (most common pattern)
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_org_user_date
    ON daily_focus_scores (org_id, user_id, date DESC);

-- Index for organization-wide reports
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_org_date
    ON daily_focus_scores (org_id, date DESC);

-- Index for user relationship (cascade deletes)
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_user_id
    ON daily_focus_scores (user_id);

-- Index for device relationship
CREATE INDEX IF NOT EXISTS ix_daily_focus_scores_device_id
    ON daily_focus_scores (device_id);
