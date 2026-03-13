using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job de agregação de dados
/// Calcula e persiste resumos diários pré-calculados (DailySummary) para acelerar consultas de relatórios
/// Idempotente: pode rodar múltiplas vezes sem duplicar dados
/// </summary>
public sealed class AggregationJob : IAggregationJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IDailySummaryRepository _dailySummaryRepository;
    private readonly ILogger<AggregationJob> _logger;

    public AggregationJob(
        TimeTrackDbContext context,
        IDailySummaryRepository dailySummaryRepository,
        ILogger<AggregationJob> logger)
    {
        _context = context;
        _dailySummaryRepository = dailySummaryRepository;
        _logger = logger;
    }

    public async Task ExecuteForDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var targetDate = date.Date;
        _logger.LogInformation("Starting aggregation job for date {Date}", targetDate);

        try
        {
            // Get all active users with activity on the target date
            var usersWithActivity = await GetUsersWithActivityAsync(targetDate, cancellationToken);

            _logger.LogInformation("Found {Count} users with activity on {Date}",
                usersWithActivity.Count, targetDate);

            var processed = 0;
            var errors = 0;

            foreach (var (userId, orgId) in usersWithActivity)
            {
                try
                {
                    await AggregateUserDayAsync(orgId, userId, targetDate, cancellationToken);
                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error aggregating data for user {UserId} on {Date}",
                        userId, targetDate);
                    errors++;
                }
            }

            _logger.LogInformation(
                "Aggregation job completed for {Date}. Processed: {Processed}, Errors: {Errors}",
                targetDate, processed, errors);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing aggregation job for date {Date}", targetDate);
            throw;
        }
    }

    public async Task ExecuteForRecentDaysAsync(int days, CancellationToken cancellationToken = default)
    {
        if (days <= 0)
            throw new ArgumentException("Days must be greater than 0", nameof(days));

        _logger.LogInformation("Starting aggregation job for last {Days} days", days);

        var endDate = DateTime.UtcNow.Date;
        var startDate = endDate.AddDays(-days);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            await ExecuteForDateAsync(date, cancellationToken);
        }

        _logger.LogInformation("Completed aggregation for {Days} days range", days);
    }

    // Method for Hangfire job (no optional parameters)
    public Task ExecuteForRecentDaysAsync(int days)
    {
        return ExecuteForRecentDaysAsync(days, CancellationToken.None);
    }

    private async Task<List<(Guid UserId, Guid OrgId)>> GetUsersWithActivityAsync(
        DateTime date,
        CancellationToken cancellationToken)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        // Get distinct users with activity sessions on the target date
        var usersWithActivity = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.StartedAt >= startOfDay && a.StartedAt < endOfDay)
            .GroupBy(a => new { a.UserId, a.OrgId })
            .Select(g => new { g.Key.UserId, g.Key.OrgId })
            .ToListAsync(cancellationToken);

        return usersWithActivity.ConvertAll(u => (u.UserId, u.OrgId));
    }

    private async Task AggregateUserDayAsync(
        Guid orgId,
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        // Aggregate activity sessions
        var sessionStats = await _context.ActivitySessions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(a => a.UserId == userId && a.StartedAt >= startOfDay && a.StartedAt < endOfDay)
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                TotalActiveSeconds = g.Sum(a => a.DurationSeconds),
                SessionCount = g.Count()
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Aggregate idle periods
        var idleStats = await _context.IdlePeriods
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(i => i.UserId == userId && i.StartedAt >= startOfDay && i.StartedAt < endOfDay)
            .GroupBy(i => i.UserId)
            .Select(g => new
            {
                TotalIdleSeconds = g.Sum(i => i.DurationSeconds)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var totalActiveSeconds = sessionStats?.TotalActiveSeconds ?? 0;
        var sessionCount = sessionStats?.SessionCount ?? 0;
        var totalIdleSeconds = idleStats?.TotalIdleSeconds ?? 0;

        // Upsert daily summary (idempotent operation)
        await _dailySummaryRepository.UpsertAsync(
            orgId,
            userId,
            date,
            totalActiveSeconds,
            totalIdleSeconds,
            sessionCount,
            cancellationToken);

        _logger.LogInformation(
            "Aggregated data for user {UserId} on {Date}: Active={ActiveSeconds}s, Idle={IdleSeconds}s, Sessions={SessionCount}",
            userId,
            date,
            totalActiveSeconds,
            totalIdleSeconds,
            sessionCount);
    }
}
