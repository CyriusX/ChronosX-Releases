using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TimeTrack.Backend.Infrastructure.Extensions;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Jobs;

namespace TimeTrack.Backend.Infrastructure.Jobs.Configuration;

/// <summary>
/// Extension methods for configuring Hangfire
/// </summary>
public static class HangfireConfiguration
{
    /// <summary>
    /// Adds Hangfire services with PostgreSQL storage
    /// </summary>
    public static IServiceCollection AddHangfireJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = GetConnectionString(configuration);

        // Configure Hangfire with PostgreSQL storage
        services.AddHangfire(config =>
        {
            config.UsePostgreSqlStorage(
                c => c.UseNpgsqlConnection(connectionString),
                new PostgreSqlStorageOptions
                {
                    // Job expiration timeout
                    InvisibilityTimeout = TimeSpan.FromMinutes(30),
                    // Queue poll interval
                    QueuePollInterval = TimeSpan.FromSeconds(15)
                });

            // Configure retry attempts for failed jobs
            config.UseFilter(new AutomaticRetryAttribute
            {
                Attempts = 3,
                DelaysInSeconds = new[] { 60, 300, 900 } // 1min, 5min, 15min
            });

            // Recommended for production
            config.UseRecommendedSerializerSettings();
        });

        // Add Hangfire server
        services.AddHangfireServer(options =>
        {
            options.ServerName = "TimeTrack-JobServer";
            options.WorkerCount = 2; // Limit concurrent jobs
            options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
        });

        // Register job implementations
        services.AddScoped<IRetentionJob, RetentionJob>();
        services.AddScoped<IAggregationJob, AggregationJob>();
        services.AddScoped<ICleanupJob, CleanupJob>();
        services.AddScoped<IFocusScoreJob, FocusScoreJob>();
        services.AddScoped<IActivitySessionConsolidationJob, ActivitySessionConsolidationJob>();
        services.AddScoped<IMachineMetricsCleanupJob, MachineMetricsCleanupJob>();

        return services;
    }

    /// <summary>
    /// Configures recurring jobs
    /// Should be called during application startup
    /// </summary>
    public static void ConfigureRecurringJobs()
    {
        // Retention job - runs daily at 03:00 UTC
        RecurringJob.AddOrUpdate<IRetentionJob>(
            "retention-job",
            job => job.ExecuteAsync(),
            "0 3 * * *", // Daily at 03:00 UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Aggregation job - runs hourly
        RecurringJob.AddOrUpdate<IAggregationJob>(
            "aggregation-job",
            job => job.ExecuteForRecentDaysAsync(1), // Process yesterday's data
            "0 * * * *", // Every hour
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Cleanup job - runs daily at 04:00 UTC
        RecurringJob.AddOrUpdate<ICleanupJob>(
            "cleanup-idempotency-keys-job",
            job => job.ExecuteAsync(),
            "0 4 * * *", // Daily at 04:00 UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Focus Score calculation job - runs daily at 00:05 UTC (processes yesterday's data)
        RecurringJob.AddOrUpdate<IFocusScoreJob>(
            "focus-score-daily",
            job => job.ExecuteForRecentDaysAsync(1), // Process yesterday's data
            "5 0 * * *", // Daily at 00:05 UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Machine Metrics Cleanup job - runs every 6 hours
        // Removes metrics older than 24h (real-time monitoring only)
        RecurringJob.AddOrUpdate<IMachineMetricsCleanupJob>(
            "machine-metrics-cleanup",
            job => job.ExecuteAsync(),
            "30 */6 * * *", // Every 6 hours at :30
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Activity Session Consolidation job - runs every 6 hours
        // Merges duplicate sessions that may have been created due to race conditions
        RecurringJob.AddOrUpdate<IActivitySessionConsolidationJob>(
            "activity-session-consolidation",
            job => job.ExecuteConsolidationAsync(), // No optional parameters - Hangfire compatible
            "0 */6 * * *", // Every 6 hours
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    private static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connectionString))
            return InfrastructureServiceCollectionExtensions.NormalizeConnectionString(connectionString);

        var databaseUrl = configuration["DATABASE_URL"];
        if (!string.IsNullOrEmpty(databaseUrl))
            return InfrastructureServiceCollectionExtensions.NormalizeConnectionString(databaseUrl);

        throw new InvalidOperationException(
            "Database connection string not found. Set DATABASE_URL or ConnectionStrings:DefaultConnection");
    }
}
