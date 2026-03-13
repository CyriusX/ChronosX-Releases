-- Migration: Add focus_mode_json column to org_policies table
-- Date: 2026-03-13
-- Description: Adds FocusMode configuration storage to organization policies

-- Add focus_mode_json column with default empty JSON object
ALTER TABLE org_policies
ADD COLUMN IF NOT EXISTS focus_mode_json jsonb NOT NULL DEFAULT '{}'::jsonb;

-- Add comment to document the column
COMMENT ON COLUMN org_policies.focus_mode_json IS 'Focus mode configuration stored as JSON (Pomodoro/Ultradian settings)';

-- Update any existing records that might have NULL value (safety measure)
UPDATE org_policies
SET focus_mode_json = '{}'::jsonb
WHERE focus_mode_json IS NULL;
