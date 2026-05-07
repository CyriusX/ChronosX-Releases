-- Subscription tables migration (idempotent)
-- Only creates tables that don't exist yet

DO $$
BEGIN
    -- subscription_plans
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'subscription_plans') THEN
        CREATE TABLE subscription_plans (
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
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            updated_at timestamp with time zone,
            CONSTRAINT PK_subscription_plans PRIMARY KEY (id)
        );
        CREATE UNIQUE INDEX IX_subscription_plans_tier ON subscription_plans (tier);
        RAISE NOTICE 'Created table: subscription_plans';
    END IF;

    -- org_subscriptions
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'org_subscriptions') THEN
        CREATE TABLE org_subscriptions (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
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
            cancel_at_period_end boolean NOT NULL DEFAULT false,
            quantity integer NOT NULL DEFAULT 1,
            created_at timestamp with time zone NOT NULL DEFAULT now(),
            updated_at timestamp with time zone,
            CONSTRAINT PK_org_subscriptions PRIMARY KEY (id),
            CONSTRAINT FK_org_subscriptions_orgs_org_id FOREIGN KEY (org_id) REFERENCES orgs (id) ON DELETE CASCADE,
            CONSTRAINT FK_org_subscriptions_subscription_plans_plan_id FOREIGN KEY (plan_id) REFERENCES subscription_plans (id) ON DELETE RESTRICT
        );
        CREATE UNIQUE INDEX IX_org_subscriptions_org_id ON org_subscriptions (org_id);
        CREATE INDEX IX_org_subscriptions_plan_id ON org_subscriptions (plan_id);
        CREATE INDEX IX_org_subscriptions_stripe_customer_id ON org_subscriptions (stripe_customer_id);
        CREATE INDEX IX_org_subscriptions_stripe_subscription_id ON org_subscriptions (stripe_subscription_id);
        RAISE NOTICE 'Created table: org_subscriptions';
    END IF;

    -- stripe_event_logs
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'stripe_event_logs') THEN
        CREATE TABLE stripe_event_logs (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            org_id uuid NOT NULL,
            stripe_event_id character varying(200) NOT NULL,
            event_type character varying(100) NOT NULL,
            processed_at timestamp with time zone NOT NULL DEFAULT now(),
            payload_hash character varying(64),
            status character varying(20) NOT NULL,
            error_message text,
            CONSTRAINT PK_stripe_event_logs PRIMARY KEY (id)
        );
        CREATE UNIQUE INDEX IX_stripe_event_logs_stripe_event_id ON stripe_event_logs (stripe_event_id);
        CREATE INDEX IX_stripe_event_logs_org_id_processed_at ON stripe_event_logs (org_id, processed_at);
        RAISE NOTICE 'Created table: stripe_event_logs';
    END IF;

    -- org_usage_records
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'org_usage_records') THEN
        CREATE TABLE org_usage_records (
            id uuid NOT NULL DEFAULT gen_random_uuid(),
            org_id uuid NOT NULL,
            active_users_count integer NOT NULL DEFAULT 0,
            active_devices_count integer NOT NULL DEFAULT 0,
            last_computed_at timestamp with time zone NOT NULL DEFAULT now(),
            CONSTRAINT PK_org_usage_records PRIMARY KEY (id)
        );
        CREATE UNIQUE INDEX IX_org_usage_records_org_id ON org_usage_records (org_id);
        RAISE NOTICE 'Created table: org_usage_records';
    END IF;

    -- Seed subscription plans
    IF NOT EXISTS (SELECT 1 FROM subscription_plans WHERE tier = 'Pro') THEN
        INSERT INTO subscription_plans (name, tier, monthly_price_cents, yearly_price_cents, max_users, max_devices,
            machine_monitoring, advanced_reports, focus_mode, api_access, priority_support, custom_categories, linear_integration, billing_analytics)
        VALUES ('Pro', 'Pro', 1200, 11520, 25, 50,
            true, true, true, true, false, true, true, false);
        RAISE NOTICE 'Seeded plan: Pro';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM subscription_plans WHERE tier = 'Enterprise') THEN
        INSERT INTO subscription_plans (name, tier, monthly_price_cents, yearly_price_cents, max_users, max_devices,
            machine_monitoring, advanced_reports, focus_mode, api_access, priority_support, custom_categories, linear_integration, billing_analytics)
        VALUES ('Enterprise', 'Enterprise', 0, NULL, 0, 0,
            true, true, true, true, true, true, true, true);
        RAISE NOTICE 'Seeded plan: Enterprise';
    END IF;

    -- Register migration in EF history
    IF NOT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20260418145616_AddSubscriptionTables') THEN
        INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('20260418145616_AddSubscriptionTables', '8.0.0');
        RAISE NOTICE 'Registered migration: 20260418145616_AddSubscriptionTables';
    END IF;

    -- Also register missing intermediate migrations
    IF NOT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20260411040000_AddBillableProjectFields') THEN
        INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('20260411040000_AddBillableProjectFields', '8.0.0');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20260411050000_AddLinearOAuthFields') THEN
        INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('20260411050000_AddLinearOAuthFields', '8.0.0');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20260412120000_AddActivitySessionUserIdIndex') THEN
        INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('20260412120000_AddActivitySessionUserIdIndex', '8.0.0');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20260415120000_ExpandTaskDescriptionTo5000') THEN
        INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('20260415120000_ExpandTaskDescriptionTo5000', '8.0.0');
    END IF;
    IF NOT EXISTS (SELECT 1 FROM __ef_migrations_history WHERE "MigrationId" = '20260414202012_AddOrgMembershipsTable') THEN
        INSERT INTO __ef_migrations_history ("MigrationId", "ProductVersion") VALUES ('20260414202012_AddOrgMembershipsTable', '8.0.0');
    END IF;

END $$;
