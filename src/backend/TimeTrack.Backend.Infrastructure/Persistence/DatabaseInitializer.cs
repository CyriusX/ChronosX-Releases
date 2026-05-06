using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.Infrastructure.Integrations.Stripe;
using TimeTrack.Backend.Infrastructure.Persistence.Seeds;

namespace TimeTrack.Backend.Infrastructure.Persistence;

public sealed class DatabaseInitializer : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DatabaseInitializer> _logger;

    public DatabaseInitializer(
        IServiceProvider serviceProvider,
        ILogger<DatabaseInitializer> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
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
            await ApplySubscriptionTablesIdempotently(dbContext, cancellationToken);
            _logger.LogInformation("Retrying remaining migrations...");
            await dbContext.Database.MigrateAsync(cancellationToken);
            _logger.LogInformation("Remaining migrations applied successfully");
        }

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

    private async Task ApplySubscriptionTablesIdempotently(
        TimeTrackDbContext dbContext, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlRawAsync(@"
            -- Subscription tables (new in this migration cycle)
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

            -- Indexes for subscription tables
            CREATE INDEX IF NOT EXISTS IX_org_subscriptions_org_id ON org_subscriptions(org_id);
            CREATE INDEX IF NOT EXISTS IX_stripe_event_logs_org_id ON stripe_event_logs(org_id);
            CREATE INDEX IF NOT EXISTS IX_stripe_event_logs_stripe_event_id ON stripe_event_logs(stripe_event_id);
            CREATE INDEX IF NOT EXISTS IX_daily_summaries_user_id ON daily_summaries(user_id);
            CREATE INDEX IF NOT EXISTS IX_daily_summaries_org_id_user_id_date ON daily_summaries(org_id, user_id, date);
            CREATE INDEX IF NOT EXISTS IX_org_usage_records_org_id ON org_usage_records(org_id);

            -- Mark the problematic migration as applied so MigrateAsync skips it on retry
            INSERT INTO ""__EFMigrationsHistory"" (""MigrationId"", ""ProductVersion"")
            VALUES ('20260418145616_AddSubscriptionTables', '8.0.0')
            ON CONFLICT DO NOTHING;
        ", cancellationToken);

        _logger.LogInformation("Subscription tables created idempotently");
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
