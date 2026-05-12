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
        services.AddScoped<IDeadlineScanJob, DeadlineScanJob>();
        services.AddScoped<IProjectPurgeJob, ProjectPurgeJob>();
        services.AddScoped<ISubscriptionStatusJob, SubscriptionStatusJob>();
        services.AddScoped<IUsageCounterRefreshJob, UsageCounterRefreshJob>();
        services.AddScoped<ITaskTimerStalePauseJob, TaskTimerStalePauseJob>();
        services.AddScoped<IDevToolsAutoRevokeJob, DevToolsAutoRevokeJob>();
        services.AddScoped<IEvidenceRetentionJob, EvidenceRetentionJob>();
        services.AddScoped<IStorageQuotaCheckJob, StorageQuotaCheckJob>();
        services.AddScoped<IOrphanCleanupJob, OrphanCleanupJob>();

        // Feature Aggregation Jobs (Fase 4)
        services.AddScoped<IWeeklyFeatureAggregationJob, WeeklyFeatureAggregationJob>();
        services.AddScoped<IMonthlyFeatureAggregationJob, MonthlyFeatureAggregationJob>();

        // AI Classification Job (Fase 4)
        services.AddScoped<IAppClassificationJob, AppClassificationJob>();

        // AI Usage Suggestion Job — per-org real usage analysis
        services.AddScoped<IAppUsageSuggestionJob, AppUsageSuggestionJob>();

        // Pattern & Anomaly Detection Jobs (Fase 4)
        services.AddScoped<PatternDetectionJob>();
        services.AddScoped<AnomalyDetectionJob>();
        services.AddScoped<WeeklyNarrativeJob>();

        // Smart Alerts & Threshold Jobs (Fase 4)
        services.AddScoped<AlertGenerationJob>();
        services.AddScoped<ThresholdUpdateJob>();

        // Live AI Insight Job
        services.AddScoped<LiveInsightJob>();

        // Weekly Email Report Job
        services.AddScoped<WeeklyEmailReportJob>();

        // Weekly Report Data Collector
        services.AddScoped<WeeklyReportDataCollector>();

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

        // Deadline scan job - runs hourly
        // Drops a DeadlineToday notification into the inbox for every assigned,
        // non-Done task whose DueDate lands on the current UTC day. Deduped per task per day.
        RecurringJob.AddOrUpdate<IDeadlineScanJob>(
            "deadline-scan",
            job => job.ExecuteAsync(),
            "0 * * * *", // Every hour on the :00
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Project purge job - runs daily at 02:00 UTC
        // Hard-deletes projects soft-deleted more than 30 days ago.
        RecurringJob.AddOrUpdate<IProjectPurgeJob>(
            "project-purge",
            job => job.ExecuteAsync(),
            "0 2 * * *", // Daily at 02:00 UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Subscription status job - runs daily at 01:00 UTC
        // Transitions past_due subscriptions with expired grace periods to unpaid
        RecurringJob.AddOrUpdate<ISubscriptionStatusJob>(
            "subscription-status-check",
            job => job.ExecuteAsync(),
            "0 1 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Usage counter refresh job - runs every 6 hours
        RecurringJob.AddOrUpdate<IUsageCounterRefreshJob>(
            "usage-counter-refresh",
            job => job.ExecuteAsync(),
            "15 */6 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Task timer stale pause watchdog - runs every 5 minutes
        // Pauses open task timers when devices haven't heartbeated recently
        RecurringJob.AddOrUpdate<ITaskTimerStalePauseJob>(
            "task-timer-stale-pause",
            job => job.ExecuteAsync(),
            "*/5 * * * *", // Every 5 minutes
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // DevTools access auto-revoke watchdog - runs every 10 minutes
        // Revokes per-user devtools flags when they expire and pushes disable commands to devices
        RecurringJob.AddOrUpdate<IDevToolsAutoRevokeJob>(
            "devtools-auto-revoke",
            job => job.ExecuteAsync(),
            "*/10 * * * *", // Every 10 minutes
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Evidence retention job - runs daily at 03:00 UTC
        // Soft-deletes expired evidence items per org policy, hard-deletes after 7 days
        RecurringJob.AddOrUpdate<IEvidenceRetentionJob>(
            "evidence-retention",
            job => job.ExecuteAsync(),
            "30 3 * * *", // Daily at 03:30 UTC (offset from retention-job)
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Storage quota check - runs daily at 04:30 UTC
        // Warns when org storage exceeds 80% of quota
        RecurringJob.AddOrUpdate<IStorageQuotaCheckJob>(
            "storage-quota-check",
            job => job.ExecuteAsync(),
            "30 4 * * *", // Daily at 04:30 UTC (offset from cleanup-job)
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Orphan cleanup job - runs weekly on Sunday at 02:00 UTC
        // Hard-deletes soft-deleted evidence items older than 7 days
        RecurringJob.AddOrUpdate<IOrphanCleanupJob>(
            "orphan-cleanup",
            job => job.ExecuteAsync(),
            "0 2 * * 0", // Sunday at 02:00 UTC
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Weekly Feature Aggregation - runs every Monday at 03:00 UTC
        RecurringJob.AddOrUpdate<IWeeklyFeatureAggregationJob>(
            "weekly-feature-aggregation",
            job => job.ExecuteAsync(),
            "0 3 * * 1",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Monthly Feature Aggregation - runs on the 1st of each month at 03:00 UTC
        RecurringJob.AddOrUpdate<IMonthlyFeatureAggregationJob>(
            "monthly-feature-aggregation",
            job => job.ExecuteAsync(),
            "0 3 1 * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // AI App Classification - runs weekly on Sunday at 22:00 UTC
        RecurringJob.AddOrUpdate<IAppClassificationJob>(
            "app-classification",
            job => job.ExecuteAsync(),
            "0 22 * * 0",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // AI Usage Suggestion - runs daily at 06:00 UTC on weekdays
        RecurringJob.AddOrUpdate<IAppUsageSuggestionJob>(
            "app-usage-suggestion",
            job => job.ExecuteAsync(),
            "0 6 * * 1-5",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Pattern Detection - runs daily at 01:00 UTC
        RecurringJob.AddOrUpdate<PatternDetectionJob>(
            "pattern-detection",
            job => job.ExecuteAsync(),
            "0 1 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Anomaly Detection - runs daily at 01:30 UTC
        RecurringJob.AddOrUpdate<AnomalyDetectionJob>(
            "anomaly-detection",
            job => job.ExecuteAsync(),
            "30 1 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Weekly Narrative - runs weekly on Sunday at 23:00 UTC
        RecurringJob.AddOrUpdate<WeeklyNarrativeJob>(
            "weekly-narrative",
            job => job.ExecuteAsync(),
            "0 23 * * 0",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Alert Generation - runs daily at 02:00 UTC
        RecurringJob.AddOrUpdate<AlertGenerationJob>(
            "alert-generation",
            job => job.ExecuteAsync(),
            "0 2 * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Threshold Update - runs weekly on Monday at 04:00 UTC
        RecurringJob.AddOrUpdate<ThresholdUpdateJob>(
            "threshold-update",
            job => job.ExecuteAsync(),
            "0 4 * * 1",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Live AI Insight - runs every 10 minutes
        RecurringJob.AddOrUpdate<LiveInsightJob>(
            "live-insight",
            job => job.ExecuteAsync(),
            "*/10 * * * *",
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        // Weekly Email Report - runs hourly to check for scheduled reports
        RecurringJob.AddOrUpdate<WeeklyEmailReportJob>(
            "weekly-email-report",
            job => job.ExecuteAsync(CancellationToken.None),
            "7 * * * *",
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
