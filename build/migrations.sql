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

