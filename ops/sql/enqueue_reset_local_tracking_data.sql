-- Enqueue a remote command to clear local tracking history/outbox on every ACTIVE device in an org.
--
-- Usage (psql):
--   \set org_id '00000000-0000-0000-0000-000000000000'
--   \set admin_user_id '00000000-0000-0000-0000-000000000000'
--   \i ops/sql/enqueue_reset_local_tracking_data.sql
--
-- This does NOT delete cloud data. It only prepares devices so a subsequent cloud wipe
-- won't be undone by agents re-uploading old local SQLite history/outbox.

INSERT INTO remote_commands (
    org_id,
    device_id,
    command_type,
    payload_json,
    status,
    created_by_user_id,
    expires_at
)
SELECT
    d.org_id,
    d.id AS device_id,
    'reset_local_tracking_data' AS command_type,
    NULL AS payload_json,
    'pending' AS status,
    :'admin_user_id'::uuid AS created_by_user_id,
    now() + interval '7 days' AS expires_at
FROM devices d
WHERE d.org_id = :'org_id'::uuid
  AND d.status = 'Active'
  AND NOT EXISTS (
      SELECT 1
      FROM remote_commands rc
      WHERE rc.device_id = d.id
        AND rc.command_type = 'reset_local_tracking_data'
        AND rc.status = 'pending'
        AND rc.expires_at > now()
  );

