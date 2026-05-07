using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http;
using Resend;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Application.Integrations;
using TimeTrack.Backend.Application.Integrations.Linear;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Integrations;
using TimeTrack.Backend.Infrastructure.Integrations.Linear;
using TimeTrack.Backend.Infrastructure.Integrations.Stripe;
using TimeTrack.Backend.Infrastructure.Jobs.Configuration;
using TimeTrack.Backend.Infrastructure.Persistence;
using TimeTrack.Backend.Infrastructure.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Services;

namespace TimeTrack.Backend.Infrastructure.Extensions;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Database
        var connectionString = GetConnectionString(configuration);

        services.AddDbContext<TimeTrackDbContext>((sp, options) =>
        {
            var currentUser = sp.GetService<Application.Common.Interfaces.ICurrentUserContext>();
            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__ef_migrations_history", "public");
                npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3);
            });
        });

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IDeviceRepository, DeviceRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IActivitySessionRepository, ActivitySessionRepository>();
        services.AddScoped<IIdlePeriodRepository, IdlePeriodRepository>();
        services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<IProjectTaskRepository, ProjectTaskRepository>();
        services.AddScoped<ITaskTimeEntryRepository, TaskTimeEntryRepository>();
        services.AddScoped<IAgentNotificationInboxRepository, AgentNotificationInboxRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IOrgPolicyRepository, OrgPolicyRepository>();
        services.AddScoped<IReportRepository, ReportRepository>();
        services.AddScoped<IDailySummaryRepository, DailySummaryRepository>();
        services.AddScoped<IDailyFocusScoreRepository, DailyFocusScoreRepository>();
        services.AddScoped<IFocusSessionRepository, FocusSessionRepository>();

        // App Category Repositories (CX-143)
        services.AddScoped<IAppCategoryGlobalRepository, AppCategoryGlobalRepository>();
        services.AddScoped<IAppCategoryOverrideRepository, AppCategoryOverrideRepository>();

        // Machine Metrics
        services.AddScoped<IMachineMetricsRepository, MachineMetricsRepository>();

        // Agent Event Logs
        services.AddScoped<IAgentEventLogRepository, AgentEventLogRepository>();

        // Remote Commands
        services.AddScoped<IRemoteCommandRepository, RemoteCommandRepository>();

        // User Integrations (Linear + future providers)
        services.AddScoped<IUserIntegrationRepository, UserIntegrationRepository>();
        services.AddScoped<ILinearSyncHistoryRepository, LinearSyncHistoryRepository>();
        services.AddSingleton<IUserIntegrationTokenProtector, UserIntegrationTokenProtector>();
        services.AddHttpClient<ILinearClient, LinearClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.linear.app/graphql");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Subscriptions & Billing
        services.Configure<PlansOptions>(configuration.GetSection("Plans"));
        services.Configure<SubscriptionOptions>(configuration.GetSection("Subscription"));
        services.AddScoped<ISubscriptionPlanRepository, SubscriptionPlanRepository>();
        services.AddScoped<IOrgSubscriptionRepository, OrgSubscriptionRepository>();
        services.AddScoped<IStripeEventLogRepository, StripeEventLogRepository>();
        services.AddScoped<IOrgUsageRecordRepository, OrgUsageRecordRepository>();
        services.AddScoped<IBillingInvoiceRepository, BillingInvoiceRepository>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<StripeWebhookProcessor>();
        services.AddScoped<IPaymentGatewayService, StripeService>();

        // Focus Score Services
        services.AddSingleton<AppProductivityClassifier>();

        // Hangfire Background Jobs
        services.AddHangfireJobs(configuration);

        // Services
        services.AddScoped<Application.Common.Interfaces.ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordGenerator, PasswordGenerator>();
        services.AddScoped<IPasswordValidator, PasswordValidator>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Email Service (Resend SDK oficial)
        services.AddOptions();
        services.AddHttpClient<ResendClient>();
        services.Configure<ResendClientOptions>(options =>
        {
            options.ApiToken =
                (string.IsNullOrWhiteSpace(configuration["Resend:ApiKey"]) ? null : configuration["Resend:ApiKey"])
                ?? configuration["RESEND_API_KEY"]
                ?? throw new InvalidOperationException("Resend API key not found. Set Resend:ApiKey or RESEND_API_KEY");
        });
        services.AddTransient<IResend, ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();

        // HttpContextAccessor for CurrentUserContext
        services.AddHttpContextAccessor();

        // Health checks
        // Simple liveness check (always healthy if app is running)
        // Database check is tagged as "ready" for more granular health monitoring
        services.AddHealthChecks()
            .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("OK"), tags: new[] { "live" })
            .AddNpgSql(connectionString, name: "database", tags: new[] { "ready" }, failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded);

        // Database initialization (seeds, migrations)
        services.AddDatabaseInitializer();

        return services;
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
