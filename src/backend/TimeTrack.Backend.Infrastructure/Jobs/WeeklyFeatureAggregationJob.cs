using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class WeeklyFeatureAggregationJob : IWeeklyFeatureAggregationJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IFeatureWeeklyRepository _weeklyRepo;
    private readonly ILogger<WeeklyFeatureAggregationJob> _logger;

    public WeeklyFeatureAggregationJob(
        TimeTrackDbContext context,
        IFeatureWeeklyRepository weeklyRepo,
        ILogger<WeeklyFeatureAggregationJob> logger)
    {
        _context = context;
        _weeklyRepo = weeklyRepo;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        // Process last complete week (Monday to Sunday)
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysSinceMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        var lastMonday = today.AddDays(-daysSinceMonday - 7);

        _logger.LogInformation("Starting WeeklyFeatureAggregation for week starting {WeekStart}", lastMonday);
        await ExecuteForWeekAsync(lastMonday, CancellationToken.None);
    }

    public async Task ExecuteForWeekAsync(DateOnly weekStart, CancellationToken ct = default)
    {
        var weekEnd = weekStart.AddDays(6);

        var users = await _context.DailyFocusScores
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Date >= weekStart && d.Date <= weekEnd)
            .GroupBy(d => new { d.UserId, d.OrgId })
            .Select(g => new { g.Key.UserId, g.Key.OrgId, DaysWithData = g.Count() })
            .Where(u => u.DaysWithData >= 3)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} users with sufficient data for week {WeekStart}", users.Count, weekStart);

        foreach (var user in users)
        {
            try
            {
                await AggregateUserWeekAsync(user.OrgId, user.UserId, weekStart, weekEnd, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating weekly features for user {UserId}", user.UserId);
            }
        }

        _logger.LogInformation("WeeklyFeatureAggregation completed for week {WeekStart}", weekStart);
    }

    private async Task AggregateUserWeekAsync(Guid orgId, Guid userId, DateOnly weekStart, DateOnly weekEnd, CancellationToken ct)
    {
        var dailyScores = await _context.DailyFocusScores
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.Date >= weekStart && d.Date <= weekEnd)
            .ToListAsync(ct);

        if (dailyScores.Count < 3)
            return;

        var totalActiveSeconds = dailyScores.Sum(d => (d.TotalTrackedMs / 1000));
        var totalProductiveSeconds = dailyScores.Sum(d => d.ProductiveSeconds);
        var totalDistractionSeconds = dailyScores.Sum(d => d.DistractionSeconds);
        var totalNeutralSeconds = dailyScores.Sum(d => d.NeutralSeconds);
        var totalAllSeconds = totalProductiveSeconds + totalDistractionSeconds + totalNeutralSeconds;

        var weekly = FeatureWeekly.Create(
            id: Guid.NewGuid(),
            orgId: orgId,
            userId: userId,
            weekStart: weekStart,
            avgFocusScore: dailyScores.Average(d => (double)d.FocusScore),
            avgProductiveRatio: totalAllSeconds > 0 ? (double)totalProductiveSeconds / totalAllSeconds : 0,
            totalActiveHours: totalActiveSeconds / 3600.0,
            avgContextSwitches: dailyScores.Average(d => (double)d.ContextSwitchesCount),
            avgInterruptionCount: dailyScores.Average(d => (double)d.InterruptionCount));

        // Calculate trends by looking at previous week
        var prevWeekStart = weekStart.AddDays(-7);
        var prevWeekEnd = prevWeekStart.AddDays(6);
        var prevWeekly = await _weeklyRepo.GetByUserAndWeekAsync(userId, prevWeekStart, ct);

        weekly.UpdateTrends(
            previousFocusScore: prevWeekly?.AvgFocusScore,
            previousProductiveRatio: prevWeekly?.AvgProductiveRatio);

        await _weeklyRepo.UpsertAsync(weekly, ct);
    }
}
