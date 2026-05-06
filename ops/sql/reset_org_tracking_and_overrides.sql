-- Reset tracking data + app category overrides for a SINGLE org.
--
-- Usage (psql):
--   \set org_id '00000000-0000-0000-0000-000000000000'
--   \i ops/sql/reset_org_tracking_and_overrides.sql
--
-- IMPORTANT:
-- 1) Deploy backend normalization first.
-- 2) Deploy the agent that understands 'reset_local_tracking_data'.
-- 3) Run ops/sql/enqueue_reset_local_tracking_data.sql and wait for devices to ack.
-- 4) Only then run this script to wipe cloud data.
--
-- This deletes historical tracking data. Run only after confirming backups/approval.

BEGIN;

-- Core tracking tables
DELETE FROM activity_sessions WHERE org_id = :'org_id'::uuid;
DELETE FROM idle_periods WHERE org_id = :'org_id'::uuid;
DELETE FROM focus_sessions WHERE org_id = :'org_id'::uuid;

-- Aggregates / reports
DELETE FROM daily_focus_scores WHERE org_id = :'org_id'::uuid;
DELETE FROM daily_summaries WHERE org_id = :'org_id'::uuid;

-- Telemetry / logs (optional but typically tracking-related)
DELETE FROM agent_event_logs WHERE org_id = :'org_id'::uuid;
DELETE FROM machine_metrics WHERE org_id = :'org_id'::uuid;

-- App category overrides (per your decision to wipe & rebuild)
DELETE FROM app_category_override WHERE org_id = :'org_id'::uuid;

-- Idempotency keys for ingest entities so a "fresh start" doesn't get blocked by old keys.
DELETE FROM idempotency_keys
WHERE org_id = :'org_id'::uuid
  AND entity_type IN ('ActivitySession', 'IdlePeriod', 'FocusSession', 'AgentEvent', 'MachineMetrics');

COMMIT;

