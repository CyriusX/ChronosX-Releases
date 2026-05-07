-- ============================================================
-- Fix Schema Sync — ChronosX Dev/Prod Database
-- Applies all missing columns, tables, indexes and records
-- from migrations that may not have been applied correctly.
--
-- 100% idempotent: safe to run multiple times.
-- Run this directly on the PostgreSQL database, then restart the API container.
-- ============================================================

BEGIN;

-- ===========================================================
-- DIAGNOSTIC: uncomment below to check current state first
-- ===========================================================
-- SELECT "MigrationId", "ProductVersion" FROM __ef_migrations_history ORDER BY "MigrationId";

-- ===========================================================
-- MIGRATION: 20260321181926_AddFilePathToActivitySession
-- ===========================================================
ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS file_path character varying(1000);

-- ===========================================================
-- MIGRATION: 20260321203000_AddAppSubcategoryToActivitySession
-- ===========================================================
ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS app_subcategory character varying(100);

-- ===========================================================
-- MIGRATION: 20260403034048_AddMachineMetricsTable
-- (table likely exists, skip)
-- ===========================================================

-- ===========================================================
-- MIGRATION: 20260403140618_AddAgentEventLogsTable
-- (table likely exists, skip)
-- ===========================================================

-- ===========================================================
-- MIGRATION: 20260403144423_AddRemoteCommandsAndDeviceInfo
-- ===========================================================
ALTER TABLE devices ADD COLUMN IF NOT EXISTS ip_address character varying(45);
ALTER TABLE devices ADD COLUMN IF NOT EXISTS os_version character varying(200);
ALTER TABLE devices ADD COLUMN IF NOT EXISTS tracking_state character varying(20);
ALTER TABLE devices ADD COLUMN IF NOT EXISTS uptime_seconds integer;

CREATE TABLE IF NOT EXISTS remote_commands (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    device_id uuid NOT NULL,
    command_type character varying(50) NOT NULL,
    payload_json character varying(2000),
    status character varying(20) NOT NULL,
    result_json character varying(2000),
    created_by_user_id uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    acknowledged_at timestamptz,
    expires_at timestamptz NOT NULL,
    CONSTRAINT PK_remote_commands PRIMARY KEY (id),
    CONSTRAINT FK_remote_commands_devices_device_id FOREIGN KEY (device_id) REFERENCES devices(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS ix_remote_commands_device_status ON remote_commands(device_id, status);
CREATE INDEX IF NOT EXISTS ix_remote_commands_org_device_created ON remote_commands(org_id, device_id, created_at DESC);

-- ===========================================================
-- MIGRATION: 20260409224255_AddDeviceHealthFields
-- ===========================================================
ALTER TABLE devices ADD COLUMN IF NOT EXISTS consecutive_sync_failures integer;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS health_status character varying(20);
ALTER TABLE devices ADD COLUMN IF NOT EXISTS ipc_connected boolean;
ALTER TABLE devices ADD COLUMN IF NOT EXISTS last_successful_sync_at timestamptz;

-- ===========================================================
-- MIGRATION: 20260410185330_AddTasksAndMembers
-- ===========================================================
ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS project_id uuid;
ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS task_id uuid;

CREATE TABLE IF NOT EXISTS agent_notification_inbox (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    user_id uuid NOT NULL,
    kind character varying(40) NOT NULL,
    title character varying(255) NOT NULL,
    body character varying(1000) NOT NULL,
    metadata_json jsonb,
    created_at timestamptz NOT NULL DEFAULT now(),
    read_at timestamptz,
    delivered_to_agent_at timestamptz,
    CONSTRAINT PK_agent_notification_inbox PRIMARY KEY (id),
    CONSTRAINT FK_agent_notification_inbox_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS project_members (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    project_id uuid NOT NULL,
    user_id uuid NOT NULL,
    role character varying(20) NOT NULL,
    added_at timestamptz NOT NULL DEFAULT now(),
    added_by_user_id uuid NOT NULL,
    CONSTRAINT PK_project_members PRIMARY KEY (id),
    CONSTRAINT FK_project_members_projects_project_id FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    CONSTRAINT FK_project_members_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS project_tasks (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    project_id uuid NOT NULL,
    title character varying(255) NOT NULL,
    description character varying(5000),
    status character varying(20) NOT NULL,
    assigned_user_id uuid,
    created_by_user_id uuid NOT NULL,
    priority character varying(10) NOT NULL,
    due_date timestamptz,
    position double precision NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz,
    moved_to_in_progress_at timestamptz,
    completed_at timestamptz,
    total_seconds_worked bigint NOT NULL DEFAULT 0,
    deleted_at timestamptz,
    xmin xid NOT NULL,
    CONSTRAINT PK_project_tasks PRIMARY KEY (id),
    CONSTRAINT FK_project_tasks_projects_project_id FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    CONSTRAINT FK_project_tasks_users_assigned_user_id FOREIGN KEY (assigned_user_id) REFERENCES users(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS task_time_entries (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    task_id uuid NOT NULL,
    user_id uuid NOT NULL,
    started_at timestamptz NOT NULL,
    ended_at timestamptz,
    duration_seconds bigint NOT NULL DEFAULT 0,
    paused_seconds bigint NOT NULL DEFAULT 0,
    paused_at timestamptz,
    source character varying(30) NOT NULL,
    CONSTRAINT PK_task_time_entries PRIMARY KEY (id),
    CONSTRAINT FK_task_time_entries_project_tasks_task_id FOREIGN KEY (task_id) REFERENCES project_tasks(id) ON DELETE CASCADE,
    CONSTRAINT FK_task_time_entries_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_activity_sessions_project_id ON activity_sessions(project_id);
CREATE INDEX IF NOT EXISTS IX_activity_sessions_task_id ON activity_sessions(task_id);
CREATE INDEX IF NOT EXISTS IX_agent_notification_inbox_org_id_user_id_created_at ON agent_notification_inbox(org_id, user_id, created_at);
CREATE INDEX IF NOT EXISTS IX_agent_notification_inbox_user_id_read_at ON agent_notification_inbox(user_id, read_at);
CREATE INDEX IF NOT EXISTS IX_project_members_org_id_user_id ON project_members(org_id, user_id);
CREATE INDEX IF NOT EXISTS IX_project_members_project_id_user_id ON project_members(project_id, user_id);
CREATE INDEX IF NOT EXISTS IX_project_members_user_id ON project_members(user_id);
CREATE INDEX IF NOT EXISTS IX_project_tasks_project_id ON project_tasks(project_id);
CREATE INDEX IF NOT EXISTS IX_task_time_entries_task_id ON task_time_entries(task_id);
CREATE INDEX IF NOT EXISTS IX_task_time_entries_user_id_ended_at ON task_time_entries(user_id, ended_at);
CREATE INDEX IF NOT EXISTS IX_task_time_entries_org_id_task_id_started_at ON task_time_entries(org_id, task_id, started_at);

-- FKs for activity_sessions -> project_tasks/projects
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_activity_sessions_project_tasks_task_id') THEN
        ALTER TABLE activity_sessions ADD CONSTRAINT FK_activity_sessions_project_tasks_task_id
            FOREIGN KEY (task_id) REFERENCES project_tasks(id) ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_activity_sessions_projects_project_id') THEN
        ALTER TABLE activity_sessions ADD CONSTRAINT FK_activity_sessions_projects_project_id
            FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE SET NULL;
    END IF;
END $$;

-- ===========================================================
-- MIGRATION: 20260411030501_AddLinearIntegrationAndDeadlines
-- ===========================================================
ALTER TABLE projects ADD COLUMN IF NOT EXISTS last_synced_at timestamptz;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS linear_project_id character varying(64);
ALTER TABLE projects ADD COLUMN IF NOT EXISTS linear_workspace_id character varying(64);
ALTER TABLE projects ADD COLUMN IF NOT EXISTS sync_source character varying(20) NOT NULL DEFAULT 'Local';

ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_issue_id character varying(64);
ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_issue_identifier character varying(32);
ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_state_id character varying(64);
ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_state_name character varying(100);
ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_team_id character varying(64);
ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_url character varying(500);

DROP INDEX IF EXISTS "IX_projects_org_id_name";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_projects_org_id_name"
    ON projects (org_id, name) WHERE sync_source = 'Local';
CREATE UNIQUE INDEX IF NOT EXISTS "IX_projects_org_id_linear_project_id"
    ON projects (org_id, linear_project_id) WHERE linear_project_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS "IX_project_tasks_org_id_linear_issue_id"
    ON project_tasks (org_id, linear_issue_id) WHERE linear_issue_id IS NOT NULL;

CREATE TABLE IF NOT EXISTS linear_sync_history (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    user_id uuid NOT NULL,
    started_at timestamptz NOT NULL,
    finished_at timestamptz NOT NULL,
    duration_ms bigint NOT NULL,
    projects_created integer NOT NULL,
    projects_updated integer NOT NULL,
    tasks_created integer NOT NULL,
    tasks_updated integer NOT NULL,
    tasks_soft_deleted integer NOT NULL,
    success boolean NOT NULL,
    error_message character varying(2000),
    CONSTRAINT PK_linear_sync_history PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS user_integrations (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    user_id uuid NOT NULL,
    provider character varying(30) NOT NULL,
    external_user_id character varying(128) NOT NULL,
    external_user_name character varying(255),
    external_user_email character varying(255),
    encrypted_token bytea NOT NULL,
    scope character varying(500),
    connected_at timestamptz NOT NULL DEFAULT now(),
    last_used_at timestamptz,
    last_sync_at timestamptz,
    status character varying(30) NOT NULL,
    error_message character varying(1000),
    metadata_json jsonb,
    CONSTRAINT PK_user_integrations PRIMARY KEY (id),
    CONSTRAINT FK_user_integrations_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_linear_sync_history_user_id_started_at ON linear_sync_history(user_id, started_at);
CREATE UNIQUE INDEX IF NOT EXISTS IX_user_integrations_user_id_provider ON user_integrations(user_id, provider);

-- ===========================================================
-- MIGRATION: 20260411040000_AddBillableProjectFields
-- ===========================================================
ALTER TABLE projects ADD COLUMN IF NOT EXISTS is_billable boolean NOT NULL DEFAULT false;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS currency character varying(3);
ALTER TABLE projects ADD COLUMN IF NOT EXISTS hourly_rate decimal(18,2);

-- ===========================================================
-- MIGRATION: 20260411050000_AddLinearOAuthFields
-- ===========================================================
ALTER TABLE user_integrations ADD COLUMN IF NOT EXISTS refresh_token bytea;
ALTER TABLE user_integrations ADD COLUMN IF NOT EXISTS token_expires_at timestamptz;
ALTER TABLE user_integrations ADD COLUMN IF NOT EXISTS auth_method integer NOT NULL DEFAULT 1;

-- ===========================================================
-- MIGRATION: 20260412120000_AddActivitySessionUserIdIndex
-- ===========================================================
CREATE INDEX IF NOT EXISTS ix_activity_sessions_user_started
    ON activity_sessions (user_id, started_at DESC);

-- ===========================================================
-- MIGRATION: 20260415120000_ExpandTaskDescriptionTo5000
-- ===========================================================
DO $$ BEGIN
    ALTER TABLE project_tasks ALTER COLUMN description TYPE character varying(5000);
EXCEPTION WHEN others THEN
    RAISE NOTICE 'project_tasks.description already varchar(5000) or table missing';
END $$;

-- ===========================================================
-- MIGRATION: 20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes
-- ===========================================================
CREATE UNIQUE INDEX IF NOT EXISTS "IX_task_time_entries_user_id_open"
    ON task_time_entries (user_id) WHERE ended_at IS NULL;

DROP INDEX IF EXISTS "IX_project_tasks_assigned_user_id_status";
DROP INDEX IF EXISTS "IX_project_tasks_org_id_project_id_status";
CREATE INDEX IF NOT EXISTS "IX_project_tasks_assigned_user_id_status_position"
    ON project_tasks (assigned_user_id, status, position);
CREATE INDEX IF NOT EXISTS "IX_project_tasks_org_id_project_id_status_position"
    ON project_tasks (org_id, project_id, status, position);

-- ===========================================================
-- MIGRATION: 20260417120000_AddProjectCreatorAndSoftDelete
-- ===========================================================
ALTER TABLE projects ADD COLUMN IF NOT EXISTS created_by_user_id uuid;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS deleted_at timestamptz;
ALTER TABLE projects ADD COLUMN IF NOT EXISTS deleted_by_user_id uuid;
CREATE INDEX IF NOT EXISTS ix_projects_deleted_at ON projects (deleted_at);

-- ===========================================================
-- MIGRATION: 20260418145616_AddSubscriptionTables
-- ===========================================================
CREATE TABLE IF NOT EXISTS subscription_plans (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    name character varying(100) NOT NULL,
    stripe_price_id character varying(200),
    stripe_product_id character varying(200),
    tier character varying(20) NOT NULL,
    monthly_price_cents integer NOT NULL,
    yearly_price_cents integer,
    max_users integer NOT NULL,
    max_devices integer NOT NULL,
    machine_monitoring boolean NOT NULL DEFAULT false,
    advanced_reports boolean NOT NULL DEFAULT false,
    focus_mode boolean NOT NULL DEFAULT false,
    api_access boolean NOT NULL DEFAULT false,
    priority_support boolean NOT NULL DEFAULT false,
    custom_categories boolean NOT NULL DEFAULT false,
    linear_integration boolean NOT NULL DEFAULT false,
    billing_analytics boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz,
    CONSTRAINT PK_subscription_plans PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS org_subscriptions (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    plan_id uuid,
    stripe_customer_id character varying(200),
    stripe_subscription_id character varying(200),
    status character varying(20) NOT NULL,
    current_period_start timestamptz,
    current_period_end timestamptz,
    trial_end timestamptz,
    grace_period_end timestamptz,
    canceled_at timestamptz,
    cancel_at_period_end boolean NOT NULL DEFAULT false,
    quantity integer NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz,
    CONSTRAINT PK_org_subscriptions PRIMARY KEY (id),
    CONSTRAINT FK_org_subscriptions_orgs_org_id FOREIGN KEY (org_id) REFERENCES orgs(id) ON DELETE CASCADE,
    CONSTRAINT FK_org_subscriptions_subscription_plans_plan_id FOREIGN KEY (plan_id) REFERENCES subscription_plans(id) ON DELETE RESTRICT
);

CREATE TABLE IF NOT EXISTS billing_invoices (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    subscription_id uuid,
    stripe_invoice_id character varying(200),
    amount_cents integer NOT NULL,
    currency character varying(3) NOT NULL DEFAULT 'USD',
    status character varying(20) NOT NULL,
    invoice_url character varying(500),
    pdf_url character varying(500),
    period_start timestamptz,
    period_end timestamptz,
    due_date timestamptz,
    paid_at timestamptz,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz,
    CONSTRAINT PK_billing_invoices PRIMARY KEY (id),
    CONSTRAINT FK_billing_invoices_org_subscriptions_subscription_id
        FOREIGN KEY (subscription_id) REFERENCES org_subscriptions(id) ON DELETE SET NULL
);

CREATE TABLE IF NOT EXISTS billing_invoice_line_items (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    invoice_id uuid NOT NULL,
    description character varying(500) NOT NULL,
    amount_cents integer NOT NULL,
    quantity integer NOT NULL DEFAULT 1,
    period_start timestamptz,
    period_end timestamptz,
    CONSTRAINT PK_billing_invoice_line_items PRIMARY KEY (id),
    CONSTRAINT FK_billing_invoice_line_items_billing_invoices_invoice_id
        FOREIGN KEY (invoice_id) REFERENCES billing_invoices(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS stripe_event_logs (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    stripe_event_id character varying(200) NOT NULL,
    event_type character varying(100) NOT NULL,
    processed_at timestamptz NOT NULL DEFAULT now(),
    payload_hash character varying(64),
    status character varying(20) NOT NULL,
    error_message text,
    CONSTRAINT PK_stripe_event_logs PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS org_usage_records (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    active_users_count integer NOT NULL DEFAULT 0,
    active_devices_count integer NOT NULL DEFAULT 0,
    last_computed_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT PK_org_usage_records PRIMARY KEY (id)
);

CREATE TABLE IF NOT EXISTS daily_summaries (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    org_id uuid NOT NULL,
    user_id uuid NOT NULL,
    date timestamptz NOT NULL,
    total_active_seconds integer NOT NULL,
    total_idle_seconds integer NOT NULL,
    session_count integer NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz,
    CONSTRAINT PK_daily_summaries PRIMARY KEY (id),
    CONSTRAINT FK_daily_summaries_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS IX_org_subscriptions_org_id ON org_subscriptions(org_id);
CREATE INDEX IF NOT EXISTS IX_stripe_event_logs_org_id ON stripe_event_logs(org_id);
CREATE INDEX IF NOT EXISTS IX_stripe_event_logs_stripe_event_id ON stripe_event_logs(stripe_event_id);
CREATE INDEX IF NOT EXISTS IX_daily_summaries_user_id ON daily_summaries(user_id);
CREATE INDEX IF NOT EXISTS IX_daily_summaries_org_id_user_id_date ON daily_summaries(org_id, user_id, date);
CREATE INDEX IF NOT EXISTS IX_org_usage_records_org_id ON org_usage_records(org_id);
CREATE INDEX IF NOT EXISTS IX_billing_invoices_org_id ON billing_invoices(org_id);
CREATE INDEX IF NOT EXISTS IX_billing_invoices_stripe_invoice_id ON billing_invoices(stripe_invoice_id);

-- ===========================================================
-- MIGRATION: 20260418185156_AddDevToolsEnabledUntilUtcToUsers
-- ===========================================================
ALTER TABLE users ADD COLUMN IF NOT EXISTS devtools_enabled_until_utc timestamptz;

-- ===========================================================
-- MIGRATION: 20260418231811_AddBillingInvoices
-- (table already created above)
-- ===========================================================

-- ===========================================================
-- MIGRATION: 20260419202623_AddRefundFieldsToBillingInvoice
-- EF Core expects PascalCase columns (no HasColumnName override in config)
-- ===========================================================
ALTER TABLE billing_invoices ADD COLUMN IF NOT EXISTS "RefundAmountCents" bigint;
ALTER TABLE billing_invoices ADD COLUMN IF NOT EXISTS "RefundId" text;
ALTER TABLE billing_invoices ADD COLUMN IF NOT EXISTS "RefundStatus" text;
ALTER TABLE billing_invoices ADD COLUMN IF NOT EXISTS "RefundedAt" timestamptz;

-- ===========================================================
-- MIGRATION: 20260420211819_AddTrialSubscriptionBackfill
-- (Data migration — skip, only needed if orgs have no subscription)
-- ===========================================================

-- ===========================================================
-- MIGRATION: 20260420224132_AddIsPlatformAdminToUsers
-- ===========================================================
ALTER TABLE users ADD COLUMN IF NOT EXISTS is_platform_admin boolean NOT NULL DEFAULT false;

-- ===========================================================
-- MIGRATION: 20260425130000_AddIdleJustificationSupport
-- *** LIKELY CULPRIT FOR THE 500 ERROR ***
-- EF Core maps IdlePeriod.JustificationReasonCode / .JustificationNote / .JustificationSubmittedAtUtc
-- When these columns are missing, every SELECT on idle_periods throws Postgres 42703
-- ===========================================================
ALTER TABLE org_policies ADD COLUMN IF NOT EXISTS idle_justification_prompt_threshold_seconds integer;
ALTER TABLE idle_periods ADD COLUMN IF NOT EXISTS justification_reason_code character varying(64);
ALTER TABLE idle_periods ADD COLUMN IF NOT EXISTS justification_note character varying(500);
ALTER TABLE idle_periods ADD COLUMN IF NOT EXISTS justification_submitted_at_utc timestamptz;

-- ===========================================================
-- SYNC MIGRATION HISTORY — mark all migrations as applied
-- ===========================================================
INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES
    ('20260310200919_InitialCreate',                       '8.0'),
    ('20260312123906_AddProjectsTable',                    '8.0'),
    ('20260313002015_CreateOrgPoliciesTable',              '8.0'),
    ('20260314214805_AddAppCategoryAndFocusScoreTables',   '8.0'),
    ('20260321181926_AddFilePathToActivitySession',        '8.0'),
    ('20260321203000_AddAppSubcategoryToActivitySession',  '8.0'),
    ('20260403034048_AddMachineMetricsTable',              '8.0'),
    ('20260403140618_AddAgentEventLogsTable',              '8.0'),
    ('20260403144423_AddRemoteCommandsAndDeviceInfo',      '8.0'),
    ('20260409224255_AddDeviceHealthFields',               '8.0'),
    ('20260410185330_AddTasksAndMembers',                  '8.0'),
    ('20260411030501_AddLinearIntegrationAndDeadlines',    '8.0'),
    ('20260411040000_AddBillableProjectFields',            '8.0'),
    ('20260411050000_AddLinearOAuthFields',                '8.0'),
    ('20260412120000_AddActivitySessionUserIdIndex',       '8.0'),
    ('20260415120000_ExpandTaskDescriptionTo5000',         '8.0'),
    ('20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes', '8.0'),
    ('20260417120000_AddProjectCreatorAndSoftDelete',      '8.0'),
    ('20260418145616_AddSubscriptionTables',               '8.0'),
    ('20260418185156_AddDevToolsEnabledUntilUtcToUsers',   '8.0'),
    ('20260418231811_AddBillingInvoices',                  '8.0'),
    ('20260419202623_AddRefundFieldsToBillingInvoice',     '8.0'),
    ('20260420211819_AddTrialSubscriptionBackfill',        '8.0'),
    ('20260420224132_AddIsPlatformAdminToUsers',           '8.0'),
    ('20260425130000_AddIdleJustificationSupport',         '8.0')
ON CONFLICT DO NOTHING;

COMMIT;

-- ============================================================
-- AFTER RUNNING THIS SCRIPT:
--   docker restart timetrack-api
-- ============================================================
