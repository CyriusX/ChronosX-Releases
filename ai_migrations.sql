CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE audit_log (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid,
        action character varying(100) NOT NULL,
        entity_type character varying(100) NOT NULL,
        entity_id uuid,
        old_values jsonb,
        new_values jsonb,
        ip_address character varying(45),
        user_agent character varying(500),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_audit_log" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE idempotency_keys (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        key character varying(64) NOT NULL,
        entity_type character varying(50) NOT NULL,
        entity_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        expires_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_idempotency_keys" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE orgs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name character varying(255) NOT NULL,
        slug character varying(100) NOT NULL,
        org_type character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_orgs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE policies (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        name character varying(255) NOT NULL,
        type character varying(50) NOT NULL,
        config_json jsonb NOT NULL,
        is_enabled boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        "OrganizationId" uuid,
        CONSTRAINT "PK_policies" PRIMARY KEY (id),
        CONSTRAINT "FK_policies_orgs_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES orgs (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE users (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        email character varying(255) NOT NULL,
        password_hash character varying(500) NOT NULL,
        display_name character varying(255) NOT NULL,
        role character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        password_must_change boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        last_login_at timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY (id),
        CONSTRAINT "FK_users_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE devices (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        hostname character varying(255) NOT NULL,
        device_name character varying(255),
        agent_version character varying(50) NOT NULL,
        display_mode character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        activated_at timestamp with time zone NOT NULL DEFAULT (now()),
        last_heartbeat_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        "OrganizationId" uuid,
        CONSTRAINT "PK_devices" PRIMARY KEY (id),
        CONSTRAINT "FK_devices_orgs_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES orgs (id),
        CONSTRAINT "FK_devices_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        device_id uuid NOT NULL,
        token_hash character varying(500) NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_refresh_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE activity_sessions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        user_id uuid NOT NULL,
        process_name character varying(255) NOT NULL,
        window_title character varying(500),
        app_category character varying(100),
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone NOT NULL,
        duration_seconds integer NOT NULL,
        idempotency_key character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_activity_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_activity_sessions_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE,
        CONSTRAINT "FK_activity_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE focus_sessions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone,
        planned_duration_minutes integer,
        actual_duration_minutes integer,
        status character varying(20) NOT NULL,
        focus_score integer,
        idempotency_key character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_focus_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_focus_sessions_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE,
        CONSTRAINT "FK_focus_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE TABLE idle_periods (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone NOT NULL,
        duration_seconds integer NOT NULL,
        idempotency_key character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_idle_periods" PRIMARY KEY (id),
        CONSTRAINT "FK_idle_periods_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE,
        CONSTRAINT "FK_idle_periods_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_activity_sessions_device_id_started_at" ON activity_sessions (device_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_activity_sessions_idempotency_key" ON activity_sessions (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_activity_sessions_org_id_user_id_started_at" ON activity_sessions (org_id, user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_activity_sessions_user_id" ON activity_sessions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_audit_log_entity_type_entity_id" ON audit_log (entity_type, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_audit_log_org_id_created_at" ON audit_log (org_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_audit_log_user_id_created_at" ON audit_log (user_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_devices_org_id_user_id" ON devices (org_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_devices_OrganizationId" ON devices ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_devices_user_id" ON devices (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_focus_sessions_device_id_started_at" ON focus_sessions (device_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_focus_sessions_idempotency_key" ON focus_sessions (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_focus_sessions_org_id_user_id_started_at" ON focus_sessions (org_id, user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_focus_sessions_user_id" ON focus_sessions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_idempotency_keys_expires_at" ON idempotency_keys (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_idempotency_keys_key" ON idempotency_keys (key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_idle_periods_device_id_started_at" ON idle_periods (device_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_idle_periods_idempotency_key" ON idle_periods (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_idle_periods_org_id_user_id_started_at" ON idle_periods (org_id, user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_idle_periods_user_id" ON idle_periods (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_orgs_slug" ON orgs (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_policies_org_id_type" ON policies (org_id, type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_policies_OrganizationId" ON policies ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_token_hash" ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE INDEX "IX_refresh_tokens_user_id_device_id" ON refresh_tokens (user_id, device_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    CREATE UNIQUE INDEX "IX_users_org_id_email" ON users (org_id, email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310200919_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260310200919_InitialCreate', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260312123906_AddProjectsTable') THEN
    CREATE TABLE password_reset_tokens (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        token_hash character varying(500) NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        used_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_password_reset_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_password_reset_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260312123906_AddProjectsTable') THEN
    CREATE TABLE projects (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        name character varying(255) NOT NULL,
        description character varying(1000),
        color character varying(7) NOT NULL DEFAULT '#4A9FFF',
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_projects" PRIMARY KEY (id),
        CONSTRAINT "FK_projects_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260312123906_AddProjectsTable') THEN
    CREATE UNIQUE INDEX "IX_password_reset_tokens_token_hash" ON password_reset_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260312123906_AddProjectsTable') THEN
    CREATE INDEX "IX_password_reset_tokens_user_id" ON password_reset_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260312123906_AddProjectsTable') THEN
    CREATE UNIQUE INDEX "IX_projects_org_id_name" ON projects (org_id, name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260312123906_AddProjectsTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260312123906_AddProjectsTable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260313002015_CreateOrgPoliciesTable') THEN
    CREATE TABLE org_policies (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        version integer NOT NULL DEFAULT 1,
        work_hours_json jsonb NOT NULL,
        app_exclusions_json jsonb NOT NULL,
        idle_threshold_seconds integer NOT NULL DEFAULT 180,
        retention_days integer NOT NULL DEFAULT 90,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_org_policies" PRIMARY KEY (id),
        CONSTRAINT "FK_org_policies_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260313002015_CreateOrgPoliciesTable') THEN
    CREATE UNIQUE INDEX "IX_org_policies_org_id" ON org_policies (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260313002015_CreateOrgPoliciesTable') THEN
    CREATE INDEX "IX_org_policies_version" ON org_policies (version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260313002015_CreateOrgPoliciesTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260313002015_CreateOrgPoliciesTable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314214805_AddAppCategoryAndFocusScoreTables') THEN

                    DO $$
                    BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns
                                       WHERE table_name = 'org_policies' AND column_name = 'focus_mode_json') THEN
                            ALTER TABLE org_policies ADD COLUMN focus_mode_json jsonb NOT NULL DEFAULT '{}'::jsonb;
                        END IF;
                    END $$;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314214805_AddAppCategoryAndFocusScoreTables') THEN

                    CREATE TABLE IF NOT EXISTS app_category_global (
                        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        identifier TEXT NOT NULL,
                        identifier_type TEXT NOT NULL CHECK (identifier_type IN ('exe', 'domain')),
                        display_name TEXT NOT NULL,
                        productivity TEXT NOT NULL CHECK (productivity IN ('productive', 'neutral', 'distraction')),
                        subcategory TEXT NOT NULL,
                        created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        CONSTRAINT uq_app_category_global_identifier UNIQUE (identifier)
                    );

                    CREATE INDEX IF NOT EXISTS ix_app_category_global_productivity ON app_category_global (productivity);
                    CREATE INDEX IF NOT EXISTS ix_app_category_global_display_name ON app_category_global (display_name);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314214805_AddAppCategoryAndFocusScoreTables') THEN

                    CREATE TABLE IF NOT EXISTS app_category_override (
                        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        org_id UUID NOT NULL REFERENCES orgs(id) ON DELETE CASCADE,
                        identifier TEXT NOT NULL,
                        identifier_type TEXT NOT NULL CHECK (identifier_type IN ('exe', 'domain')),
                        display_name TEXT,
                        productivity TEXT NOT NULL CHECK (productivity IN ('productive', 'neutral', 'distraction')),
                        subcategory TEXT NOT NULL,
                        note TEXT,
                        created_by UUID NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                        created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        CONSTRAINT uq_app_category_override_org_identifier UNIQUE (org_id, identifier)
                    );

                    CREATE INDEX IF NOT EXISTS ix_app_category_override_org_id ON app_category_override (org_id);
                    CREATE INDEX IF NOT EXISTS ix_app_category_override_org_productivity ON app_category_override (org_id, productivity);
                    CREATE INDEX IF NOT EXISTS IX_app_category_override_created_by ON app_category_override (created_by);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314214805_AddAppCategoryAndFocusScoreTables') THEN

                    CREATE TABLE IF NOT EXISTS daily_focus_scores (
                        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        org_id UUID NOT NULL,
                        user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                        device_id UUID REFERENCES devices(id) ON DELETE SET NULL,
                        date DATE NOT NULL,
                        total_tracked_ms BIGINT NOT NULL DEFAULT 0,
                        focus_time_ms BIGINT NOT NULL DEFAULT 0,
                        distraction_ms BIGINT NOT NULL DEFAULT 0,
                        distraction_count INTEGER NOT NULL DEFAULT 0,
                        pause_count INTEGER NOT NULL DEFAULT 0,
                        idle_count INTEGER NOT NULL DEFAULT 0,
                        long_focus_block_count INTEGER NOT NULL DEFAULT 0,
                        focus_score SMALLINT NOT NULL,
                        calculated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        created_at TIMESTAMPTZ NOT NULL DEFAULT now()
                    );

                    CREATE UNIQUE INDEX IF NOT EXISTS IX_daily_focus_scores_org_id_user_id_date
                        ON daily_focus_scores (org_id, user_id, date DESC);
                    CREATE INDEX IF NOT EXISTS IX_daily_focus_scores_org_id_date
                        ON daily_focus_scores (org_id, date DESC);
                    CREATE INDEX IF NOT EXISTS IX_daily_focus_scores_user_id
                        ON daily_focus_scores (user_id);
                    CREATE INDEX IF NOT EXISTS IX_daily_focus_scores_device_id
                        ON daily_focus_scores (device_id);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314214805_AddAppCategoryAndFocusScoreTables') THEN

                    CREATE TABLE IF NOT EXISTS daily_summaries (
                        id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
                        org_id UUID NOT NULL,
                        user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
                        date TIMESTAMPTZ NOT NULL,
                        total_active_seconds INTEGER NOT NULL,
                        total_idle_seconds INTEGER NOT NULL,
                        session_count INTEGER NOT NULL,
                        created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
                        updated_at TIMESTAMPTZ
                    );

                    CREATE INDEX IF NOT EXISTS IX_daily_summaries_org_id_date
                        ON daily_summaries (org_id, date);
                    CREATE UNIQUE INDEX IF NOT EXISTS IX_daily_summaries_user_id_date
                        ON daily_summaries (user_id, date);
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260314214805_AddAppCategoryAndFocusScoreTables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260314214805_AddAppCategoryAndFocusScoreTables', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321181926_AddFilePathToActivitySession') THEN
    ALTER TABLE idle_periods ALTER COLUMN id DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321181926_AddFilePathToActivitySession') THEN
    ALTER TABLE focus_sessions ALTER COLUMN id DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321181926_AddFilePathToActivitySession') THEN
    ALTER TABLE activity_sessions ALTER COLUMN id DROP DEFAULT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321181926_AddFilePathToActivitySession') THEN
    ALTER TABLE activity_sessions ADD file_path character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321181926_AddFilePathToActivitySession') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321181926_AddFilePathToActivitySession', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403034048_AddMachineMetricsTable') THEN
    CREATE TABLE machine_metrics (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        cpu_percent double precision NOT NULL,
        memory_used_mb bigint NOT NULL,
        memory_total_mb bigint NOT NULL,
        disk_used_gb double precision NOT NULL,
        disk_total_gb double precision NOT NULL,
        sampled_at_utc timestamp with time zone NOT NULL,
        idempotency_key character varying(128) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_machine_metrics" PRIMARY KEY (id),
        CONSTRAINT "FK_machine_metrics_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403034048_AddMachineMetricsTable') THEN
    CREATE INDEX ix_machine_metrics_device_sampled ON machine_metrics (device_id, sampled_at_utc DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403034048_AddMachineMetricsTable') THEN
    CREATE INDEX ix_machine_metrics_org_id ON machine_metrics (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403034048_AddMachineMetricsTable') THEN
    CREATE INDEX ix_machine_metrics_sampled_at ON machine_metrics (sampled_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403034048_AddMachineMetricsTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260403034048_AddMachineMetricsTable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    CREATE TABLE agent_event_logs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        event_type character varying(100) NOT NULL,
        category character varying(50) NOT NULL,
        severity character varying(20) NOT NULL,
        message character varying(1000) NOT NULL,
        metadata_json character varying(4000),
        timestamp_utc timestamp with time zone NOT NULL,
        idempotency_key character varying(128) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_agent_event_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_agent_event_logs_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    CREATE INDEX ix_agent_event_logs_category_type ON agent_event_logs (category, event_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    CREATE INDEX ix_agent_event_logs_device_timestamp ON agent_event_logs (device_id, timestamp_utc DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    CREATE INDEX ix_agent_event_logs_org_id ON agent_event_logs (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    CREATE INDEX ix_agent_event_logs_severity ON agent_event_logs (severity);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    CREATE INDEX ix_agent_event_logs_timestamp ON agent_event_logs (timestamp_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403140618_AddAgentEventLogsTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260403140618_AddAgentEventLogsTable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    ALTER TABLE devices ADD ip_address character varying(45);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    ALTER TABLE devices ADD os_version character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    ALTER TABLE devices ADD tracking_state character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    ALTER TABLE devices ADD uptime_seconds integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    CREATE TABLE remote_commands (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        command_type character varying(50) NOT NULL,
        payload_json character varying(2000),
        status character varying(20) NOT NULL,
        result_json character varying(2000),
        created_by_user_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        acknowledged_at timestamp with time zone,
        expires_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_remote_commands" PRIMARY KEY (id),
        CONSTRAINT "FK_remote_commands_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    CREATE INDEX ix_remote_commands_device_status ON remote_commands (device_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    CREATE INDEX ix_remote_commands_org_device_created ON remote_commands (org_id, device_id, created_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260403144423_AddRemoteCommandsAndDeviceInfo') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260403144423_AddRemoteCommandsAndDeviceInfo', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409224255_AddDeviceHealthFields') THEN
    ALTER TABLE devices ADD COLUMN IF NOT EXISTS consecutive_sync_failures integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409224255_AddDeviceHealthFields') THEN
    ALTER TABLE devices ADD COLUMN IF NOT EXISTS health_status character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409224255_AddDeviceHealthFields') THEN
    ALTER TABLE devices ADD COLUMN IF NOT EXISTS ipc_connected boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409224255_AddDeviceHealthFields') THEN
    ALTER TABLE devices ADD COLUMN IF NOT EXISTS last_successful_sync_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260409224255_AddDeviceHealthFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260409224255_AddDeviceHealthFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS project_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    ALTER TABLE activity_sessions ADD COLUMN IF NOT EXISTS task_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE TABLE agent_notification_inbox (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        kind character varying(40) NOT NULL,
        title character varying(255) NOT NULL,
        body character varying(1000) NOT NULL,
        metadata_json jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        read_at timestamp with time zone,
        delivered_to_agent_at timestamp with time zone,
        CONSTRAINT "PK_agent_notification_inbox" PRIMARY KEY (id),
        CONSTRAINT "FK_agent_notification_inbox_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE TABLE project_members (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        project_id uuid NOT NULL,
        user_id uuid NOT NULL,
        role character varying(20) NOT NULL,
        added_at timestamp with time zone NOT NULL DEFAULT (now()),
        added_by_user_id uuid NOT NULL,
        CONSTRAINT "PK_project_members" PRIMARY KEY (id),
        CONSTRAINT "FK_project_members_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
        CONSTRAINT "FK_project_members_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE TABLE project_tasks (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        project_id uuid NOT NULL,
        title character varying(255) NOT NULL,
        description character varying(2000),
        status character varying(20) NOT NULL,
        assigned_user_id uuid,
        created_by_user_id uuid NOT NULL,
        priority character varying(10) NOT NULL,
        due_date timestamp with time zone,
        position double precision NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        moved_to_in_progress_at timestamp with time zone,
        completed_at timestamp with time zone,
        total_seconds_worked bigint NOT NULL,
        deleted_at timestamp with time zone,
        CONSTRAINT "PK_project_tasks" PRIMARY KEY (id),
        CONSTRAINT "FK_project_tasks_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
        CONSTRAINT "FK_project_tasks_users_assigned_user_id" FOREIGN KEY (assigned_user_id) REFERENCES users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE TABLE task_time_entries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        task_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone,
        duration_seconds bigint NOT NULL,
        paused_seconds bigint NOT NULL,
        paused_at timestamp with time zone,
        source character varying(30) NOT NULL,
        CONSTRAINT "PK_task_time_entries" PRIMARY KEY (id),
        CONSTRAINT "FK_task_time_entries_project_tasks_task_id" FOREIGN KEY (task_id) REFERENCES project_tasks (id) ON DELETE CASCADE,
        CONSTRAINT "FK_task_time_entries_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_activity_sessions_project_id" ON activity_sessions (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_activity_sessions_task_id" ON activity_sessions (task_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_agent_notification_inbox_org_id_user_id_created_at" ON agent_notification_inbox (org_id, user_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_agent_notification_inbox_user_id_read_at" ON agent_notification_inbox (user_id, read_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_project_members_org_id_user_id" ON project_members (org_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE UNIQUE INDEX "IX_project_members_project_id_user_id" ON project_members (project_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_project_members_user_id" ON project_members (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_project_tasks_assigned_user_id_status" ON project_tasks (assigned_user_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_project_tasks_org_id_project_id_status" ON project_tasks (org_id, project_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_project_tasks_project_id" ON project_tasks (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_task_time_entries_org_id_task_id_started_at" ON task_time_entries (org_id, task_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_task_time_entries_task_id" ON task_time_entries (task_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    CREATE INDEX "IX_task_time_entries_user_id_ended_at" ON task_time_entries (user_id, ended_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    ALTER TABLE activity_sessions ADD CONSTRAINT "FK_activity_sessions_project_tasks_task_id" FOREIGN KEY (task_id) REFERENCES project_tasks (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    ALTER TABLE activity_sessions ADD CONSTRAINT "FK_activity_sessions_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260410185330_AddTasksAndMembers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260410185330_AddTasksAndMembers', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS last_synced_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS linear_project_id character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS linear_workspace_id character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS sync_source character varying(20) NOT NULL DEFAULT 'Local';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_issue_id character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_issue_identifier character varying(32);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_state_id character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_state_name character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_team_id character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    ALTER TABLE project_tasks ADD COLUMN IF NOT EXISTS linear_url character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    DROP INDEX IF EXISTS "IX_projects_org_id_name";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_projects_org_id_name" ON projects (org_id, name) WHERE sync_source = 'Local';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_projects_org_id_linear_project_id" ON projects (org_id, linear_project_id) WHERE linear_project_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_project_tasks_org_id_linear_issue_id" ON project_tasks (org_id, linear_issue_id) WHERE linear_issue_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE TABLE linear_sync_history (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        finished_at timestamp with time zone NOT NULL,
        duration_ms bigint NOT NULL,
        projects_created integer NOT NULL,
        projects_updated integer NOT NULL,
        tasks_created integer NOT NULL,
        tasks_updated integer NOT NULL,
        tasks_soft_deleted integer NOT NULL,
        success boolean NOT NULL,
        error_message character varying(2000),
        CONSTRAINT "PK_linear_sync_history" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE TABLE user_integrations (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        provider character varying(30) NOT NULL,
        external_user_id character varying(128) NOT NULL,
        external_user_name character varying(255),
        external_user_email character varying(255),
        encrypted_token bytea NOT NULL,
        scope character varying(500),
        connected_at timestamp with time zone NOT NULL DEFAULT (now()),
        last_used_at timestamp with time zone,
        last_sync_at timestamp with time zone,
        status character varying(30) NOT NULL,
        error_message character varying(1000),
        metadata_json jsonb,
        CONSTRAINT "PK_user_integrations" PRIMARY KEY (id),
        CONSTRAINT "FK_user_integrations_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE INDEX "IX_linear_sync_history_user_id_started_at" ON linear_sync_history (user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    CREATE UNIQUE INDEX "IX_user_integrations_user_id_provider" ON user_integrations (user_id, provider);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260411030501_AddLinearIntegrationAndDeadlines') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260411030501_AddLinearIntegrationAndDeadlines', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes') THEN

                WITH ranked AS (
                    SELECT
                        id,
                        user_id,
                        ROW_NUMBER() OVER (PARTITION BY user_id ORDER BY started_at DESC, id DESC) AS rn
                    FROM task_time_entries
                    WHERE ended_at IS NULL
                )
                UPDATE task_time_entries t
                SET
                    ended_at = t.started_at,
                    duration_seconds = 0,
                    paused_seconds = 0,
                    paused_at = NULL
                FROM ranked r
                WHERE t.id = r.id
                  AND r.rn > 1;
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes') THEN

                CREATE UNIQUE INDEX IF NOT EXISTS "IX_task_time_entries_user_id_open"
                ON task_time_entries (user_id)
                WHERE ended_at IS NULL;
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes') THEN

                DROP INDEX IF EXISTS "IX_project_tasks_assigned_user_id_status";
                DROP INDEX IF EXISTS "IX_project_tasks_org_id_project_id_status";

                CREATE INDEX IF NOT EXISTS "IX_project_tasks_assigned_user_id_status_position"
                ON project_tasks (assigned_user_id, status, position);

                CREATE INDEX IF NOT EXISTS "IX_project_tasks_org_id_project_id_status_position"
                ON project_tasks (org_id, project_id, status, position);
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417120000_AddProjectCreatorAndSoftDelete') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS created_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417120000_AddProjectCreatorAndSoftDelete') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS deleted_at timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417120000_AddProjectCreatorAndSoftDelete') THEN
    ALTER TABLE projects ADD COLUMN IF NOT EXISTS deleted_by_user_id uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417120000_AddProjectCreatorAndSoftDelete') THEN
    CREATE INDEX IF NOT EXISTS ix_projects_deleted_at ON projects (deleted_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417120000_AddProjectCreatorAndSoftDelete') THEN

                UPDATE projects p
                SET created_by_user_id = (
                    SELECT pm.user_id
                    FROM project_members pm
                    WHERE pm.project_id = p.id
                    ORDER BY (pm.role = 'Owner') DESC, pm.added_at ASC, pm.id ASC
                    LIMIT 1
                )
                WHERE p.created_by_user_id IS NULL;
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260417120000_AddProjectCreatorAndSoftDelete') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260417120000_AddProjectCreatorAndSoftDelete', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE app_category_global (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        identifier character varying(255) NOT NULL,
        identifier_type character varying(10) NOT NULL,
        display_name character varying(100) NOT NULL,
        productivity character varying(20) NOT NULL,
        subcategory character varying(30) NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_app_category_global" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE audit_log (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid,
        action character varying(100) NOT NULL,
        entity_type character varying(100) NOT NULL,
        entity_id uuid,
        old_values jsonb,
        new_values jsonb,
        ip_address character varying(45),
        user_agent character varying(500),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_audit_log" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE idempotency_keys (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        key character varying(64) NOT NULL,
        entity_type character varying(50) NOT NULL,
        entity_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        expires_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_idempotency_keys" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE linear_sync_history (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        finished_at timestamp with time zone NOT NULL,
        duration_ms bigint NOT NULL,
        projects_created integer NOT NULL,
        projects_updated integer NOT NULL,
        tasks_created integer NOT NULL,
        tasks_updated integer NOT NULL,
        tasks_soft_deleted integer NOT NULL,
        success boolean NOT NULL,
        error_message character varying(2000),
        CONSTRAINT "PK_linear_sync_history" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE org_policies (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        version integer NOT NULL DEFAULT 1,
        work_hours_json jsonb NOT NULL,
        app_exclusions_json jsonb NOT NULL,
        focus_mode_json jsonb NOT NULL DEFAULT ('{}'::jsonb),
        idle_threshold_seconds integer NOT NULL DEFAULT 180,
        retention_days integer NOT NULL DEFAULT 90,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_org_policies" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE org_usage_records (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        active_users_count integer NOT NULL DEFAULT 0,
        active_devices_count integer NOT NULL DEFAULT 0,
        last_computed_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_org_usage_records" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE orgs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name character varying(255) NOT NULL,
        slug character varying(100) NOT NULL,
        org_type character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_orgs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE stripe_event_logs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        stripe_event_id character varying(200) NOT NULL,
        event_type character varying(100) NOT NULL,
        processed_at timestamp with time zone NOT NULL DEFAULT (now()),
        payload_hash character varying(64),
        status character varying(20) NOT NULL,
        error_message text,
        CONSTRAINT "PK_stripe_event_logs" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE subscription_plans (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        name character varying(100) NOT NULL,
        stripe_price_id character varying(200),
        stripe_product_id character varying(200),
        tier character varying(20) NOT NULL,
        monthly_price_cents integer NOT NULL,
        yearly_price_cents integer,
        max_users integer NOT NULL,
        max_devices integer NOT NULL,
        machine_monitoring boolean NOT NULL DEFAULT FALSE,
        advanced_reports boolean NOT NULL DEFAULT FALSE,
        focus_mode boolean NOT NULL DEFAULT FALSE,
        api_access boolean NOT NULL DEFAULT FALSE,
        priority_support boolean NOT NULL DEFAULT FALSE,
        custom_categories boolean NOT NULL DEFAULT FALSE,
        linear_integration boolean NOT NULL DEFAULT FALSE,
        billing_analytics boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_subscription_plans" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE policies (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        name character varying(255) NOT NULL,
        type character varying(50) NOT NULL,
        config_json jsonb NOT NULL,
        is_enabled boolean NOT NULL DEFAULT TRUE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        "OrganizationId" uuid,
        CONSTRAINT "PK_policies" PRIMARY KEY (id),
        CONSTRAINT "FK_policies_orgs_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES orgs (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE projects (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        created_by_user_id uuid,
        name character varying(255) NOT NULL,
        description character varying(1000),
        color character varying(7) NOT NULL DEFAULT '#4A9FFF',
        status character varying(20) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        deleted_at timestamp with time zone,
        deleted_by_user_id uuid,
        is_billable boolean NOT NULL DEFAULT FALSE,
        currency character varying(3),
        hourly_rate numeric(18,2),
        sync_source character varying(20) NOT NULL DEFAULT 'Local',
        linear_project_id character varying(64),
        linear_workspace_id character varying(64),
        last_synced_at timestamp with time zone,
        CONSTRAINT "PK_projects" PRIMARY KEY (id),
        CONSTRAINT "FK_projects_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE users (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        email character varying(255) NOT NULL,
        password_hash character varying(500) NOT NULL,
        display_name character varying(255) NOT NULL,
        role character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        password_must_change boolean NOT NULL DEFAULT FALSE,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        last_login_at timestamp with time zone,
        CONSTRAINT "PK_users" PRIMARY KEY (id),
        CONSTRAINT "FK_users_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE org_subscriptions (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        plan_id uuid,
        stripe_customer_id character varying(200),
        stripe_subscription_id character varying(200),
        status character varying(20) NOT NULL,
        current_period_start timestamp with time zone,
        current_period_end timestamp with time zone,
        trial_end timestamp with time zone,
        grace_period_end timestamp with time zone,
        canceled_at timestamp with time zone,
        cancel_at_period_end boolean NOT NULL DEFAULT FALSE,
        quantity integer NOT NULL DEFAULT 1,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_org_subscriptions" PRIMARY KEY (id),
        CONSTRAINT "FK_org_subscriptions_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE CASCADE,
        CONSTRAINT "FK_org_subscriptions_subscription_plans_plan_id" FOREIGN KEY (plan_id) REFERENCES subscription_plans (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE agent_notification_inbox (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        kind character varying(40) NOT NULL,
        title character varying(255) NOT NULL,
        body character varying(1000) NOT NULL,
        metadata_json jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        read_at timestamp with time zone,
        delivered_to_agent_at timestamp with time zone,
        CONSTRAINT "PK_agent_notification_inbox" PRIMARY KEY (id),
        CONSTRAINT "FK_agent_notification_inbox_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE app_category_override (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        identifier character varying(255) NOT NULL,
        identifier_type character varying(10) NOT NULL,
        display_name character varying(100),
        productivity character varying(20) NOT NULL,
        subcategory character varying(30) NOT NULL,
        note character varying(500),
        created_by uuid NOT NULL,
        created_at timestamp with time zone NOT NULL,
        updated_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_app_category_override" PRIMARY KEY (id),
        CONSTRAINT "FK_app_category_override_orgs_org_id" FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE CASCADE,
        CONSTRAINT "FK_app_category_override_users_created_by" FOREIGN KEY (created_by) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE daily_summaries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        date timestamp with time zone NOT NULL,
        total_active_seconds integer NOT NULL,
        total_idle_seconds integer NOT NULL,
        session_count integer NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        CONSTRAINT "PK_daily_summaries" PRIMARY KEY (id),
        CONSTRAINT "FK_daily_summaries_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE devices (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        hostname character varying(255) NOT NULL,
        device_name character varying(255),
        agent_version character varying(50) NOT NULL,
        display_mode character varying(20) NOT NULL,
        status character varying(20) NOT NULL,
        activated_at timestamp with time zone NOT NULL DEFAULT (now()),
        last_heartbeat_at timestamp with time zone,
        os_version character varying(200),
        ip_address character varying(45),
        uptime_seconds integer,
        tracking_state character varying(20),
        health_status character varying(20),
        consecutive_sync_failures integer,
        last_successful_sync_at timestamp with time zone,
        ipc_connected boolean,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        "OrganizationId" uuid,
        CONSTRAINT "PK_devices" PRIMARY KEY (id),
        CONSTRAINT "FK_devices_orgs_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES orgs (id),
        CONSTRAINT "FK_devices_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE password_reset_tokens (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        token_hash character varying(500) NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        used_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_password_reset_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_password_reset_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE project_members (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        project_id uuid NOT NULL,
        user_id uuid NOT NULL,
        role character varying(20) NOT NULL,
        added_at timestamp with time zone NOT NULL DEFAULT (now()),
        added_by_user_id uuid NOT NULL,
        CONSTRAINT "PK_project_members" PRIMARY KEY (id),
        CONSTRAINT "FK_project_members_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
        CONSTRAINT "FK_project_members_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE project_tasks (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        project_id uuid NOT NULL,
        title character varying(255) NOT NULL,
        description character varying(5000),
        status character varying(20) NOT NULL,
        assigned_user_id uuid,
        created_by_user_id uuid NOT NULL,
        priority character varying(10) NOT NULL,
        due_date timestamp with time zone,
        position double precision NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        updated_at timestamp with time zone,
        moved_to_in_progress_at timestamp with time zone,
        completed_at timestamp with time zone,
        total_seconds_worked bigint NOT NULL,
        deleted_at timestamp with time zone,
        linear_issue_id character varying(64),
        linear_issue_identifier character varying(32),
        linear_url character varying(500),
        linear_state_id character varying(64),
        linear_state_name character varying(100),
        linear_team_id character varying(64),
        CONSTRAINT "PK_project_tasks" PRIMARY KEY (id),
        CONSTRAINT "FK_project_tasks_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE CASCADE,
        CONSTRAINT "FK_project_tasks_users_assigned_user_id" FOREIGN KEY (assigned_user_id) REFERENCES users (id) ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE refresh_tokens (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        device_id uuid NOT NULL,
        token_hash character varying(500) NOT NULL,
        expires_at timestamp with time zone NOT NULL,
        revoked_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_refresh_tokens" PRIMARY KEY (id),
        CONSTRAINT "FK_refresh_tokens_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE user_integrations (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        provider character varying(30) NOT NULL,
        external_user_id character varying(128) NOT NULL,
        external_user_name character varying(255),
        external_user_email character varying(255),
        encrypted_token bytea NOT NULL,
        refresh_token bytea,
        token_expires_at timestamp with time zone,
        auth_method integer NOT NULL DEFAULT 1,
        scope character varying(500),
        connected_at timestamp with time zone NOT NULL,
        last_used_at timestamp with time zone,
        last_sync_at timestamp with time zone,
        status character varying(30) NOT NULL,
        error_message character varying(1000),
        metadata_json jsonb,
        CONSTRAINT "PK_user_integrations" PRIMARY KEY (id),
        CONSTRAINT "FK_user_integrations_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE agent_event_logs (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        event_type character varying(100) NOT NULL,
        category character varying(50) NOT NULL,
        severity character varying(20) NOT NULL,
        message character varying(1000) NOT NULL,
        metadata_json character varying(4000),
        timestamp_utc timestamp with time zone NOT NULL,
        idempotency_key character varying(128) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_agent_event_logs" PRIMARY KEY (id),
        CONSTRAINT "FK_agent_event_logs_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE daily_focus_scores (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        device_id uuid,
        date date NOT NULL,
        total_tracked_ms bigint NOT NULL DEFAULT 0,
        focus_time_ms bigint NOT NULL DEFAULT 0,
        distraction_ms bigint NOT NULL DEFAULT 0,
        distraction_count integer NOT NULL DEFAULT 0,
        pause_count integer NOT NULL DEFAULT 0,
        idle_count integer NOT NULL DEFAULT 0,
        long_focus_block_count integer NOT NULL DEFAULT 0,
        focus_score smallint NOT NULL,
        calculated_at timestamp with time zone NOT NULL DEFAULT (now()),
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_daily_focus_scores" PRIMARY KEY (id),
        CONSTRAINT "FK_daily_focus_scores_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE SET NULL,
        CONSTRAINT "FK_daily_focus_scores_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE focus_sessions (
        id uuid NOT NULL,
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone,
        planned_duration_minutes integer,
        actual_duration_minutes integer,
        status character varying(20) NOT NULL,
        focus_score integer,
        idempotency_key character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_focus_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_focus_sessions_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE,
        CONSTRAINT "FK_focus_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE idle_periods (
        id uuid NOT NULL,
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone NOT NULL,
        duration_seconds integer NOT NULL,
        idempotency_key character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_idle_periods" PRIMARY KEY (id),
        CONSTRAINT "FK_idle_periods_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE,
        CONSTRAINT "FK_idle_periods_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE machine_metrics (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        cpu_percent double precision NOT NULL,
        memory_used_mb bigint NOT NULL,
        memory_total_mb bigint NOT NULL,
        disk_used_gb double precision NOT NULL,
        disk_total_gb double precision NOT NULL,
        sampled_at_utc timestamp with time zone NOT NULL,
        idempotency_key character varying(128) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_machine_metrics" PRIMARY KEY (id),
        CONSTRAINT "FK_machine_metrics_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE remote_commands (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        command_type character varying(50) NOT NULL,
        payload_json character varying(2000),
        status character varying(20) NOT NULL,
        result_json character varying(2000),
        created_by_user_id uuid NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        acknowledged_at timestamp with time zone,
        expires_at timestamp with time zone NOT NULL,
        CONSTRAINT "PK_remote_commands" PRIMARY KEY (id),
        CONSTRAINT "FK_remote_commands_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE activity_sessions (
        id uuid NOT NULL,
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        user_id uuid NOT NULL,
        process_name character varying(255) NOT NULL,
        window_title character varying(500),
        file_path character varying(1000),
        app_category character varying(100),
        app_subcategory character varying(100),
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone NOT NULL,
        duration_seconds integer NOT NULL,
        idempotency_key character varying(64) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        project_id uuid,
        task_id uuid,
        CONSTRAINT "PK_activity_sessions" PRIMARY KEY (id),
        CONSTRAINT "FK_activity_sessions_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE CASCADE,
        CONSTRAINT "FK_activity_sessions_project_tasks_task_id" FOREIGN KEY (task_id) REFERENCES project_tasks (id) ON DELETE SET NULL,
        CONSTRAINT "FK_activity_sessions_projects_project_id" FOREIGN KEY (project_id) REFERENCES projects (id) ON DELETE SET NULL,
        CONSTRAINT "FK_activity_sessions_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE TABLE task_time_entries (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        task_id uuid NOT NULL,
        user_id uuid NOT NULL,
        started_at timestamp with time zone NOT NULL,
        ended_at timestamp with time zone,
        duration_seconds bigint NOT NULL,
        paused_seconds bigint NOT NULL,
        paused_at timestamp with time zone,
        source character varying(30) NOT NULL,
        CONSTRAINT "PK_task_time_entries" PRIMARY KEY (id),
        CONSTRAINT "FK_task_time_entries_project_tasks_task_id" FOREIGN KEY (task_id) REFERENCES project_tasks (id) ON DELETE CASCADE,
        CONSTRAINT "FK_task_time_entries_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_activity_sessions_device_id_started_at" ON activity_sessions (device_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_activity_sessions_idempotency_key" ON activity_sessions (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_activity_sessions_org_id_user_id_started_at" ON activity_sessions (org_id, user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_activity_sessions_project_id" ON activity_sessions (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_activity_sessions_task_id" ON activity_sessions (task_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_activity_sessions_user_started ON activity_sessions (user_id, started_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_agent_event_logs_category_type ON agent_event_logs (category, event_type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_agent_event_logs_device_timestamp ON agent_event_logs (device_id, timestamp_utc DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_agent_event_logs_org_id ON agent_event_logs (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_agent_event_logs_severity ON agent_event_logs (severity);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_agent_event_logs_timestamp ON agent_event_logs (timestamp_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_agent_notification_inbox_org_id_user_id_created_at" ON agent_notification_inbox (org_id, user_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_agent_notification_inbox_user_id_read_at" ON agent_notification_inbox (user_id, read_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_app_category_global_display_name ON app_category_global (display_name);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_app_category_global_productivity ON app_category_global (productivity);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX uq_app_category_global_identifier ON app_category_global (identifier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_app_category_override_created_by" ON app_category_override (created_by);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_app_category_override_org_id ON app_category_override (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_app_category_override_org_productivity ON app_category_override (org_id, productivity);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX uq_app_category_override_org_identifier ON app_category_override (org_id, identifier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_audit_log_entity_type_entity_id" ON audit_log (entity_type, entity_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_audit_log_org_id_created_at" ON audit_log (org_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_audit_log_user_id_created_at" ON audit_log (user_id, created_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_daily_focus_scores_device_id" ON daily_focus_scores (device_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_daily_focus_scores_org_id_date" ON daily_focus_scores (org_id, date DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_daily_focus_scores_org_id_user_id_date" ON daily_focus_scores (org_id, user_id, date DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_daily_focus_scores_user_id" ON daily_focus_scores (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_daily_summaries_org_id_date" ON daily_summaries (org_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_daily_summaries_user_id_date" ON daily_summaries (user_id, date);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_devices_org_id_user_id" ON devices (org_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_devices_OrganizationId" ON devices ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_devices_user_id" ON devices (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_focus_sessions_device_id_started_at" ON focus_sessions (device_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_focus_sessions_idempotency_key" ON focus_sessions (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_focus_sessions_org_id_user_id_started_at" ON focus_sessions (org_id, user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_focus_sessions_user_id" ON focus_sessions (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_idempotency_keys_expires_at" ON idempotency_keys (expires_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_idempotency_keys_key" ON idempotency_keys (key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_idle_periods_device_id_started_at" ON idle_periods (device_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_idle_periods_idempotency_key" ON idle_periods (idempotency_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_idle_periods_org_id_user_id_started_at" ON idle_periods (org_id, user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_idle_periods_user_id" ON idle_periods (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_linear_sync_history_user_id_started_at" ON linear_sync_history (user_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_machine_metrics_device_sampled ON machine_metrics (device_id, sampled_at_utc DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_machine_metrics_org_id ON machine_metrics (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_machine_metrics_sampled_at ON machine_metrics (sampled_at_utc);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_org_policies_org_id" ON org_policies (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_org_policies_version" ON org_policies (version);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_org_subscriptions_org_id" ON org_subscriptions (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_org_subscriptions_plan_id" ON org_subscriptions (plan_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_org_subscriptions_stripe_customer_id" ON org_subscriptions (stripe_customer_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_org_subscriptions_stripe_subscription_id" ON org_subscriptions (stripe_subscription_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_org_usage_records_org_id" ON org_usage_records (org_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_orgs_slug" ON orgs (slug);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_password_reset_tokens_token_hash" ON password_reset_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_password_reset_tokens_user_id" ON password_reset_tokens (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_policies_org_id_type" ON policies (org_id, type);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_policies_OrganizationId" ON policies ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_project_members_org_id_user_id" ON project_members (org_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_project_members_project_id_user_id" ON project_members (project_id, user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_project_members_user_id" ON project_members (user_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_project_tasks_assigned_user_id_status_position" ON project_tasks (assigned_user_id, status, position);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_project_tasks_org_id_linear_issue_id" ON project_tasks (org_id, linear_issue_id) WHERE linear_issue_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_project_tasks_org_id_project_id_status_position" ON project_tasks (org_id, project_id, status, position);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_project_tasks_project_id" ON project_tasks (project_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_projects_org_id_linear_project_id" ON projects (org_id, linear_project_id) WHERE linear_project_id IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_projects_org_id_name" ON projects (org_id, name) WHERE sync_source = 'Local';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_refresh_tokens_token_hash" ON refresh_tokens (token_hash);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_refresh_tokens_user_id_device_id" ON refresh_tokens (user_id, device_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_remote_commands_device_status ON remote_commands (device_id, status);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX ix_remote_commands_org_device_created ON remote_commands (org_id, device_id, created_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_stripe_event_logs_org_id_processed_at" ON stripe_event_logs (org_id, processed_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_stripe_event_logs_stripe_event_id" ON stripe_event_logs (stripe_event_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_subscription_plans_tier" ON subscription_plans (tier);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_task_time_entries_org_id_task_id_started_at" ON task_time_entries (org_id, task_id, started_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_task_time_entries_task_id" ON task_time_entries (task_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE INDEX "IX_task_time_entries_user_id_ended_at" ON task_time_entries (user_id, ended_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_task_time_entries_user_id_open" ON task_time_entries (user_id) WHERE ended_at IS NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_user_integrations_user_id_provider" ON user_integrations (user_id, provider);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    CREATE UNIQUE INDEX "IX_users_org_id_email" ON users (org_id, email);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260418145616_AddSubscriptionTables', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418185156_AddDevToolsEnabledUntilUtcToUsers') THEN

                    ALTER TABLE users
                    ADD COLUMN IF NOT EXISTS devtools_enabled_until_utc timestamptz NULL;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418185156_AddDevToolsEnabledUntilUtcToUsers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260418185156_AddDevToolsEnabledUntilUtcToUsers', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418231811_AddBillingInvoices') THEN

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
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260418231811_AddBillingInvoices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260418231811_AddBillingInvoices', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419202623_AddRefundFieldsToBillingInvoice') THEN

                    DO $$ BEGIN
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundAmountCents') THEN
                            ALTER TABLE billing_invoices ADD COLUMN "RefundAmountCents" bigint NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundId') THEN
                            ALTER TABLE billing_invoices ADD COLUMN "RefundId" text NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundStatus') THEN
                            ALTER TABLE billing_invoices ADD COLUMN "RefundStatus" text NULL;
                        END IF;
                        IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'billing_invoices' AND column_name = 'RefundedAt') THEN
                            ALTER TABLE billing_invoices ADD COLUMN "RefundedAt" timestamp with time zone NULL;
                        END IF;
                    END $$;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260419202623_AddRefundFieldsToBillingInvoice') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260419202623_AddRefundFieldsToBillingInvoice', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420211819_AddTrialSubscriptionBackfill') THEN

                    INSERT INTO org_subscriptions
                        (id, org_id, plan_id, status, quantity, trial_end, created_at, updated_at)
                    SELECT
                        gen_random_uuid(),
                        o.id,
                        COALESCE(
                            (SELECT id FROM subscription_plans WHERE tier = 'Free' LIMIT 1),
                            (SELECT id FROM subscription_plans WHERE tier = 'Pro'  LIMIT 1)
                        ),
                        'Trialing',
                        1,
                        now() + interval '14 days',
                        now(),
                        now()
                    FROM orgs o
                    WHERE NOT EXISTS (
                        SELECT 1 FROM org_subscriptions s WHERE s.org_id = o.id
                    );
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420211819_AddTrialSubscriptionBackfill') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260420211819_AddTrialSubscriptionBackfill', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420224132_AddIsPlatformAdminToUsers') THEN
    ALTER TABLE users ADD is_platform_admin boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260420224132_AddIsPlatformAdminToUsers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260420224132_AddIsPlatformAdminToUsers', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423184243_AddEvidencePolicyFields') THEN
    ALTER TABLE org_policies ADD evidence_retention_days integer NOT NULL DEFAULT 30;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423184243_AddEvidencePolicyFields') THEN
    ALTER TABLE org_policies ADD screenshot_excluded_apps_json jsonb NOT NULL DEFAULT ('[]'::jsonb);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423184243_AddEvidencePolicyFields') THEN
    ALTER TABLE org_policies ADD screenshot_interval_minutes integer NOT NULL DEFAULT 5;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423184243_AddEvidencePolicyFields') THEN
    ALTER TABLE org_policies ADD screenshots_enabled boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423184243_AddEvidencePolicyFields') THEN
    ALTER TABLE org_policies ADD website_tracking_enabled boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423184243_AddEvidencePolicyFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260423184243_AddEvidencePolicyFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE TABLE evidence_items (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid NOT NULL,
        org_id uuid NOT NULL,
        device_id uuid NOT NULL,
        evidence_type character varying(50) NOT NULL,
        storage_key character varying(500) NOT NULL,
        external_media_id character varying(100),
        captured_at timestamp with time zone NOT NULL,
        app_name character varying(200) NOT NULL,
        window_title_hash character varying(128),
        file_size_bytes bigint NOT NULL DEFAULT 0,
        is_deleted boolean NOT NULL DEFAULT FALSE,
        deleted_at timestamp with time zone,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_evidence_items" PRIMARY KEY (id),
        CONSTRAINT "FK_evidence_items_devices_device_id" FOREIGN KEY (device_id) REFERENCES devices (id) ON DELETE RESTRICT,
        CONSTRAINT "FK_evidence_items_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE TABLE storage_keys (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        bucket character varying(100) NOT NULL,
        key character varying(500) NOT NULL,
        region character varying(50) NOT NULL,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        "OrganizationId" uuid,
        CONSTRAINT "PK_storage_keys" PRIMARY KEY (id),
        CONSTRAINT "FK_storage_keys_orgs_OrganizationId" FOREIGN KEY ("OrganizationId") REFERENCES orgs (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE INDEX "IX_evidence_items_device_id" ON evidence_items (device_id);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE INDEX "IX_evidence_items_is_deleted_captured_at" ON evidence_items (is_deleted, captured_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE INDEX "IX_evidence_items_org_id_captured_at" ON evidence_items (org_id, captured_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE UNIQUE INDEX "IX_evidence_items_storage_key" ON evidence_items (storage_key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE INDEX "IX_evidence_items_user_id_captured_at" ON evidence_items (user_id, captured_at);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE INDEX "IX_storage_keys_org_id_key" ON storage_keys (org_id, key);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    CREATE INDEX "IX_storage_keys_OrganizationId" ON storage_keys ("OrganizationId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423190525_AddEvidenceItemsAndStorageKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260423190525_AddEvidenceItemsAndStorageKeys', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423201010_AddStorageQuotaFields') THEN
    ALTER TABLE orgs ADD storage_quota_gb numeric(10,2) NOT NULL DEFAULT 10.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423201010_AddStorageQuotaFields') THEN
    ALTER TABLE orgs ADD storage_used_bytes bigint NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260423201010_AddStorageQuotaFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260423201010_AddStorageQuotaFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD productive_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD distraction_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD neutral_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD context_switches_count integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD interruption_count integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD top_app_exe character varying(512);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD top_app_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD distinct_apps_count integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD browser_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD productivity_ratio double precision NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD focus_sessions_count integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD focus_sessions_completed integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD longest_focus_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    ALTER TABLE daily_focus_scores ADD avg_focus_seconds integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE TABLE feature_weekly (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        week_start date NOT NULL,
        avg_focus_score double precision NOT NULL,
        avg_productive_ratio double precision NOT NULL,
        total_active_hours double precision NOT NULL,
        avg_context_switches double precision NOT NULL,
        avg_interruption_count double precision NOT NULL,
        trend_focus_score double precision,
        trend_productive_ratio double precision,
        computed_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_feature_weekly" PRIMARY KEY (id),
        CONSTRAINT "FK_feature_weekly_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE UNIQUE INDEX "IX_feature_weekly_user_id_week_start" ON feature_weekly (user_id, week_start);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE INDEX "IX_feature_weekly_org_id_week_start" ON feature_weekly (org_id, week_start);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE TABLE feature_monthly (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        org_id uuid NOT NULL,
        user_id uuid NOT NULL,
        month_start date NOT NULL,
        avg_focus_score double precision NOT NULL,
        avg_productive_ratio double precision NOT NULL,
        total_active_hours double precision NOT NULL,
        trend_focus_score double precision,
        computed_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_feature_monthly" PRIMARY KEY (id),
        CONSTRAINT "FK_feature_monthly_users_user_id" FOREIGN KEY (user_id) REFERENCES users (id) ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE UNIQUE INDEX "IX_feature_monthly_user_id_month_start" ON feature_monthly (user_id, month_start);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE INDEX "IX_feature_monthly_org_id_month_start" ON feature_monthly (org_id, month_start);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE TABLE ai_decision_log (
        id uuid NOT NULL DEFAULT (gen_random_uuid()),
        user_id uuid,
        org_id uuid NOT NULL,
        decision_type character varying(100) NOT NULL,
        input_data jsonb NOT NULL,
        output jsonb NOT NULL,
        model_version character varying(50) NOT NULL,
        confidence double precision,
        tokens_used integer,
        latency_ms integer,
        was_reviewed boolean NOT NULL DEFAULT FALSE,
        review_outcome character varying(50),
        correct_value jsonb,
        created_at timestamp with time zone NOT NULL DEFAULT (now()),
        CONSTRAINT "PK_ai_decision_log" PRIMARY KEY (id)
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE INDEX "IX_ai_decision_log_user_id_decision_type_created_at" ON ai_decision_log (user_id, decision_type, created_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE INDEX "IX_ai_decision_log_org_id_decision_type_created_at" ON ai_decision_log (org_id, decision_type, created_at DESC);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    CREATE INDEX "IX_ai_decision_log_was_reviewed_decision_type" ON ai_decision_log (was_reviewed, decision_type) WHERE was_reviewed = false;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260425154636_AddAiModulePhase1') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260425154636_AddAiModulePhase1', '8.0.0');
    END IF;
END $EF$;
COMMIT;


-- ============================================================
-- Manual Migration: 20260425170000_AddPatternAndAnomalyTables
-- ============================================================
START TRANSACTION;

CREATE TABLE IF NOT EXISTS user_patterns (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    org_id uuid NOT NULL,
    pattern_tag character varying(100) NOT NULL,
    detected_at date NOT NULL,
    strength double precision NOT NULL,
    evidence jsonb NOT NULL,
    description text,
    is_active boolean NOT NULL DEFAULT true,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT PK_user_patterns PRIMARY KEY (id);

CREATE UNIQUE INDEX IF NOT EXISTS IX_user_patterns_user_id_pattern_tag_detected_at
    ON user_patterns (user_id, pattern_tag, detected_at);

CREATE INDEX IF NOT EXISTS IX_user_patterns_user_id_is_active_detected_at
    ON user_patterns (user_id, is_active, detected_at);

CREATE TABLE IF NOT EXISTS behavioral_anomalies (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    org_id uuid NOT NULL,
    anomaly_type character varying(100) NOT NULL,
    severity character varying(20) NOT NULL,
    detected_at date NOT NULL,
    evidence jsonb NOT NULL,
    baseline_value double precision,
    actual_value double precision,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT PK_behavioral_anomalies PRIMARY KEY (id);

CREATE INDEX IF NOT EXISTS IX_behavioral_anomalies_user_id_detected_at
    ON behavioral_anomalies (user_id, detected_at);

CREATE INDEX IF NOT EXISTS IX_behavioral_anomalies_org_id_severity_detected_at
    ON behavioral_anomalies (org_id, severity, detected_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260425170000_AddPatternAndAnomalyTables', '8.0.0')
ON CONFLICT DO NOTHING;

COMMIT;

-- ============================================================
-- Manual Migration: 20260425190000_AddSmartAlertsTable
-- ============================================================
START TRANSACTION;

CREATE TABLE IF NOT EXISTS smart_alerts (
    id uuid NOT NULL DEFAULT gen_random_uuid(),
    user_id uuid NOT NULL,
    about_user_id uuid,
    org_id uuid NOT NULL,
    alert_type character varying(100) NOT NULL,
    message text NOT NULL,
    severity character varying(20) NOT NULL,
    action_type character varying(50),
    was_read boolean NOT NULL DEFAULT false,
    was_acted boolean NOT NULL DEFAULT false,
    created_at timestamp with time zone NOT NULL DEFAULT now(),
    CONSTRAINT PK_smart_alerts PRIMARY KEY (id);

CREATE INDEX IF NOT EXISTS IX_smart_alerts_user_id_was_read_created_at
    ON smart_alerts (user_id, was_read, created_at);

CREATE INDEX IF NOT EXISTS IX_smart_alerts_org_id_severity_created_at
    ON smart_alerts (org_id, severity, created_at);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260425190000_AddSmartAlertsTable', '8.0.0')
ON CONFLICT DO NOTHING;

COMMIT;
