using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Resend;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.FocusScore;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
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
            options.ApiToken = configuration["Resend:ApiKey"]
                ?? configuration["RESEND_API_KEY"]
                ?? throw new InvalidOperationException("Resend API key not found. Set Resend:ApiKey or RESEND_API_KEY");
        });
        services.AddTransient<IResend, ResendClient>();
        services.AddScoped<IEmailService, ResendEmailService>();

        // HttpContextAccessor for CurrentUserContext
        services.AddHttpContextAccessor();

        // Health checks
        services.AddHealthChecks()
            .AddNpgSql(connectionString, name: "database", tags: new[] { "ready" });

        // Database initialization (seeds, migrations)
        services.AddDatabaseInitializer();

        return services;
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        // Support Neon DATABASE_URL format
        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrEmpty(databaseUrl))
        {
            return ConvertNeonUrlToConnectionString(databaseUrl);
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string not found. Set DATABASE_URL or ConnectionStrings:DefaultConnection");
        }

        return connectionString;
    }

    private static string ConvertNeonUrlToConnectionString(string databaseUrl)
    {
        // Parse Neon URL format: postgres://user:password@host:port/database?sslmode=require
        var uri = new Uri(databaseUrl);
        var userInfo = uri.UserInfo.Split(':');

        var builder = new Npgsql.NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port,
            Username = userInfo[0],
            Password = userInfo.Length > 1 ? userInfo[1] : string.Empty,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = uri.Query.Contains("sslmode=require")
                ? Npgsql.SslMode.Require
                : Npgsql.SslMode.Prefer
        };

        return builder.ToString();
    }
}
