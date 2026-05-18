using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TimeTrack.Backend.Infrastructure.Integrations.Stripe;
using TimeTrack.Backend.Infrastructure.Persistence.Seeds;

namespace TimeTrack.Backend.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;
    private readonly string _connectionString;

    private static readonly string[] AllMigrationIds =
    [
        "20260310200919_InitialCreate",
        "20260312123906_AddProjectsTable",
        "20260313002015_CreateOrgPoliciesTable",
        "20260314214805_AddAppCategoryAndFocusScoreTables",
        "20260321181926_AddFilePathToActivitySession",
        "20260321203000_AddAppSubcategoryToActivitySession",
        "20260403034048_AddMachineMetricsTable",
        "20260403140618_AddAgentEventLogsTable",
        "20260403144423_AddRemoteCommandsAndDeviceInfo",
        "20260409224255_AddDeviceHealthFields",
        "20260410185330_AddTasksAndMembers",
        "20260411030501_AddLinearIntegrationAndDeadlines",
        "20260411040000_AddBillableProjectFields",
        "20260411050000_AddLinearOAuthFields",
        "20260412120000_AddActivitySessionUserIdIndex",
        "20260415120000_ExpandTaskDescriptionTo5000",
        "20260416120000_AddTaskEntryOpenUniqueAndTaskIndexes",
        "20260417120000_AddProjectCreatorAndSoftDelete",
        "20260418145616_AddSubscriptionTables",
        "20260418185156_AddDevToolsEnabledUntilUtcToUsers",
        "20260418231811_AddBillingInvoices",
        "20260419202623_AddRefundFieldsToBillingInvoice",
        "20260420211819_AddTrialSubscriptionBackfill",
        "20260420224132_AddIsPlatformAdminToUsers",
        "20260425130000_AddIdleJustificationSupport",
        "20260507000000_AddOrgInviteLinksAndOnboarding",
    ];

    public DatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializer> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _connectionString = GetConnectionString(configuration);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting database initialization...");

        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TimeTrackDbContext>();

        try
        {
            _logger.LogInformation("Applying database migrations...");
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Database migrations applied successfully");
        }
        catch (Exception ex) when (IsDuplicateTableError(ex))
        {
            _logger.LogWarning(ex, "Migration conflict (tables already exist) — applying idempotent fallback");
            await ApplyAllMissingTablesAndMarkMigrationsApplied(dbContext, cancellationToken);
        }

        // Manual migrations (no .Designer.cs) are invisible to EF Core reflection discovery and
        // never applied by MigrateAsync. Run their SQL idempotently on every startup using raw
        // NpgsqlConnection to avoid EF Core's String.Format placeholder interpretation.
        await ApplyManualMigrationsAsync(cancellationToken);

        _logger.LogInformation("Running seed data...");
        await AppCategoryGlobalSeed.SeedAsync(dbContext, cancellationToken);
        var plansOptions = scope.ServiceProvider.GetRequiredService<IOptions<PlansOptions>>();
        await SubscriptionPlanSeed.SeedAsync(dbContext, plansOptions, cancellationToken);
        _logger.LogInformation("Seed data completed");

        _logger.LogInformation("Database initialization completed successfully");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static bool IsDuplicateTableError(Exception ex)
    {
        return ex.InnerException?.Message?.Contains("already exists") == true ||
               ex.Message?.Contains("already exists") == true;
    }

    private async Task ApplyManualMigrationsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Ensuring manual migration schema is current...");

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var sql = @"
            CREATE TABLE IF NOT EXISTS ""__EFMigrationsHistory"" (
                ""MigrationId"" character varying(150) NOT NULL,
                ""ProductVersion"" character varying(32) NOT NULL,
                CONSTRAINT ""PK___EFMigrationsHistory"" PRIMARY KEY (""MigrationId"")
            );

            CREATE TABLE IF NOT EXISTS org_invite_links (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                org_id uuid NOT NULL REFERENCES orgs(id) ON DELETE RESTRICT,
                token_hash character varying(500) NOT NULL,
                role character varying(20) NOT NULL DEFAULT 'Colaborador',
                created_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                expires_at timestamp with time zone,
                max_uses integer,
                use_count integer NOT NULL DEFAULT 0,
                is_active boolean NOT NULL DEFAULT true,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT PK_org_invite_links PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_org_invite_links_token_hash ON org_invite_links(token_hash);
            CREATE INDEX IF NOT EXISTS ix_org_invite_links_org_id ON org_invite_links(org_id);

            ALTER TABLE orgs ADD COLUMN IF NOT EXISTS onboarding_completed_at timestamp with time zone;

            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260507000000_AddOrgInviteLinksAndOnboarding', '8.0.0')
            ON CONFLICT DO NOTHING;

            CREATE TABLE IF NOT EXISTS platform_api_keys (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                label character varying(200) NOT NULL,
                key_hash character varying(128) NOT NULL,
                created_at_utc timestamp with time zone NOT NULL DEFAULT now(),
                revoked_at_utc timestamp with time zone,
                last_used_at_utc timestamp with time zone,
                CONSTRAINT PK_platform_api_keys PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ux_platform_api_keys_key_hash ON platform_api_keys(key_hash);
            CREATE INDEX IF NOT EXISTS ix_platform_api_keys_created_at_utc ON platform_api_keys(created_at_utc);

            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260513170000_AddPlatformApiKeys', '8.0.0')
            ON CONFLICT DO NOTHING;

            CREATE TABLE IF NOT EXISTS ops_device_issue_states (
                device_id uuid NOT NULL REFERENCES devices(id) ON DELETE CASCADE,
                org_id uuid NOT NULL REFERENCES orgs(id) ON DELETE CASCADE,
                issue character varying(20) NOT NULL DEFAULT 'none',
                is_active boolean NOT NULL DEFAULT false,
                last_transition_at_utc timestamp with time zone NOT NULL DEFAULT now(),
                last_notified_at_utc timestamp with time zone,
                CONSTRAINT PK_ops_device_issue_states PRIMARY KEY (device_id)
            );

            CREATE INDEX IF NOT EXISTS ix_ops_device_issue_states_org_id ON ops_device_issue_states(org_id);

            CREATE TABLE IF NOT EXISTS platform_event_logs (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                event_type character varying(100) NOT NULL,
                severity character varying(20) NOT NULL,
                message character varying(1000) NOT NULL,
                metadata_json character varying(4000),
                timestamp_utc timestamp with time zone NOT NULL,
                idempotency_key character varying(128) NOT NULL,
                created_at_utc timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT PK_platform_event_logs PRIMARY KEY (id)
            );

            CREATE INDEX IF NOT EXISTS ix_platform_event_logs_timestamp ON platform_event_logs(timestamp_utc);
            CREATE INDEX IF NOT EXISTS ix_platform_event_logs_severity ON platform_event_logs(severity);
            CREATE UNIQUE INDEX IF NOT EXISTS ux_platform_event_logs_idempotency_key ON platform_event_logs(idempotency_key);

            CREATE TABLE IF NOT EXISTS platform_health_state (
                id uuid NOT NULL,
                status character varying(20) NOT NULL DEFAULT 'healthy',
                checks_json character varying(4000) NOT NULL DEFAULT '{}',
                last_changed_at_utc timestamp with time zone NOT NULL DEFAULT now(),
                updated_at_utc timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT PK_platform_health_state PRIMARY KEY (id)
            );

            INSERT INTO platform_health_state (id)
            VALUES ('00000000-0000-0000-0000-000000000001'::uuid)
            ON CONFLICT (id) DO NOTHING;

            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260513233000_AddOpsMonitoringTables', '8.0.0')
            ON CONFLICT DO NOTHING;
        ";

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogInformation("Manual migration schema up to date");
    }

    private async Task ApplyAllMissingTablesAndMarkMigrationsApplied(
        TimeTrackDbContext dbContext, CancellationToken cancellationToken)
    {
        // Create any genuinely new tables that might be missing
        await dbContext.Database.ExecuteSqlRawAsync(@"
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
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                updated_at timestamp with time zone,
                CONSTRAINT PK_subscription_plans PRIMARY KEY (id)
            );

            CREATE TABLE IF NOT EXISTS stripe_event_logs (
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

            CREATE TABLE IF NOT EXISTS org_usage_records (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                org_id uuid NOT NULL,
                active_users_count integer NOT NULL DEFAULT 0,
                active_devices_count integer NOT NULL DEFAULT 0,
                last_computed_at timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT PK_org_usage_records PRIMARY KEY (id)
            );

            CREATE TABLE IF NOT EXISTS org_subscriptions (
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
                CONSTRAINT FK_org_subscriptions_orgs_org_id FOREIGN KEY (org_id) REFERENCES orgs(id) ON DELETE CASCADE,
                CONSTRAINT FK_org_subscriptions_subscription_plans_plan_id FOREIGN KEY (plan_id) REFERENCES subscription_plans(id) ON DELETE RESTRICT
            );

            CREATE TABLE IF NOT EXISTS daily_summaries (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                org_id uuid NOT NULL,
                user_id uuid NOT NULL,
                date timestamp with time zone NOT NULL,
                total_active_seconds integer NOT NULL,
                total_idle_seconds integer NOT NULL,
                session_count integer NOT NULL,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                updated_at timestamp with time zone,
                CONSTRAINT PK_daily_summaries PRIMARY KEY (id),
                CONSTRAINT FK_daily_summaries_users_user_id FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
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
                period_start timestamp with time zone,
                period_end timestamp with time zone,
                due_date timestamp with time zone,
                paid_at timestamp with time zone,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                updated_at timestamp with time zone,
                refund_amount_cents integer,
                refund_reason character varying(200),
                refunded_at timestamp with time zone,
                CONSTRAINT PK_billing_invoices PRIMARY KEY (id),
                CONSTRAINT FK_billing_invoices_org_subscriptions_subscription_id FOREIGN KEY (subscription_id) REFERENCES org_subscriptions(id) ON DELETE SET NULL
            );

            CREATE TABLE IF NOT EXISTS billing_invoice_line_items (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                invoice_id uuid NOT NULL,
                description character varying(500) NOT NULL,
                amount_cents integer NOT NULL,
                quantity integer NOT NULL DEFAULT 1,
                period_start timestamp with time zone,
                period_end timestamp with time zone,
                CONSTRAINT PK_billing_invoice_line_items PRIMARY KEY (id),
                CONSTRAINT FK_billing_invoice_line_items_billing_invoices_invoice_id FOREIGN KEY (invoice_id) REFERENCES billing_invoices(id) ON DELETE CASCADE
            );

            CREATE INDEX IF NOT EXISTS IX_org_subscriptions_org_id ON org_subscriptions(org_id);
            CREATE INDEX IF NOT EXISTS IX_stripe_event_logs_org_id ON stripe_event_logs(org_id);
            CREATE INDEX IF NOT EXISTS IX_stripe_event_logs_stripe_event_id ON stripe_event_logs(stripe_event_id);
            CREATE INDEX IF NOT EXISTS IX_daily_summaries_user_id ON daily_summaries(user_id);
            CREATE INDEX IF NOT EXISTS IX_daily_summaries_org_id_user_id_date ON daily_summaries(org_id, user_id, date);
            CREATE INDEX IF NOT EXISTS IX_org_usage_records_org_id ON org_usage_records(org_id);
            CREATE INDEX IF NOT EXISTS IX_billing_invoices_org_id ON billing_invoices(org_id);
            CREATE INDEX IF NOT EXISTS IX_billing_invoices_stripe_invoice_id ON billing_invoices(stripe_invoice_id);

            CREATE TABLE IF NOT EXISTS org_invite_links (
                id uuid NOT NULL DEFAULT gen_random_uuid(),
                org_id uuid NOT NULL REFERENCES orgs(id) ON DELETE RESTRICT,
                token_hash character varying(500) NOT NULL,
                role character varying(20) NOT NULL DEFAULT 'Colaborador',
                created_by_user_id uuid NOT NULL REFERENCES users(id) ON DELETE RESTRICT,
                expires_at timestamp with time zone,
                max_uses integer,
                use_count integer NOT NULL DEFAULT 0,
                is_active boolean NOT NULL DEFAULT true,
                created_at timestamp with time zone NOT NULL DEFAULT now(),
                CONSTRAINT PK_org_invite_links PRIMARY KEY (id)
            );

            CREATE UNIQUE INDEX IF NOT EXISTS ix_org_invite_links_token_hash ON org_invite_links(token_hash);
            CREATE INDEX IF NOT EXISTS ix_org_invite_links_org_id ON org_invite_links(org_id);

            ALTER TABLE orgs ADD COLUMN IF NOT EXISTS onboarding_completed_at timestamp with time zone;

        ", cancellationToken);

        _logger.LogInformation("Missing tables created idempotently");

        // Mark ALL migrations as applied so MigrateAsync won't retry
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            _logger.LogInformation("Marking {Count} pending migrations as applied", pending.Count);
            var values = string.Join(", ", pending.Select(id => $"'{id}', '8.0.0'"));
            var sql = $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES {string.Join(", ", pending.Select(id => $"('{id}', '8.0.0')"))} ON CONFLICT DO NOTHING";
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
            foreach (var migrationId in pending)
                _logger.LogInformation("Marked migration {MigrationId} as applied", migrationId);
        }
        else
        {
            _logger.LogInformation("No pending migrations to mark");
        }

        _logger.LogInformation("Idempotent fallback completed");
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        // First check for explicit connection string (takes priority)
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
            return NormalizeConnectionString(connectionString);

        // Support DATABASE_URL format (URI style)
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrEmpty(databaseUrl))
            return NormalizeConnectionString(databaseUrl);

        throw new InvalidOperationException(
            "Database connection string not found. Set DATABASE_URL or ConnectionStrings:DefaultConnection");
    }

    internal static string NormalizeConnectionString(string value)
    {
        if (value.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            value.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return ConvertUriToConnectionString(value);
        }
        return value;
    }

    private static string ConvertUriToConnectionString(string databaseUrl)
    {
        // Parse URI format: postgres://user:password@host:port/database?sslmode=disable
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        // Parse sslmode query param
        var query = uri.Query.TrimStart('?');
        var sslMode = Npgsql.SslMode.Prefer;
        foreach (var part in query.Split('&'))
        {
            if (part.StartsWith("sslmode=", StringComparison.OrdinalIgnoreCase))
            {
                var val = part.Substring("sslmode=".Length);
                sslMode = val.ToLowerInvariant() switch
                {
                    "disable" => Npgsql.SslMode.Disable,
                    "require" => Npgsql.SslMode.Require,
                    "verify-ca" => Npgsql.SslMode.VerifyCA,
                    "verify-full" => Npgsql.SslMode.VerifyFull,
                    _ => Npgsql.SslMode.Prefer
                };
            }
        }

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = sslMode
        };

        return builder.ToString();
    }
}

public static class DatabaseInitializerExtensions
{
    public static IServiceCollection AddDatabaseInitializer(this IServiceCollection services)
    {
        services.AddHostedService<DatabaseInitializer>();
        return services;
    }
}
