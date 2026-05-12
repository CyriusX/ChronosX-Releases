using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class MonthlyFeatureAggregationJob : IMonthlyFeatureAggregationJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IFeatureMonthlyRepository _monthlyRepo;
    private readonly ILogger<MonthlyFeatureAggregationJob> _logger;

    public MonthlyFeatureAggregationJob(
        TimeTrackDbContext context,
        IFeatureMonthlyRepository monthlyRepo,
        ILogger<MonthlyFeatureAggregationJob> logger)
    {
        _context = context;
        _monthlyRepo = monthlyRepo;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        // Process last complete month
        var now = DateTime.UtcNow;
        var lastMonth = now.Month == 1 ? 12 : now.Month - 1;
        var lastMonthYear = now.Month == 1 ? now.Year - 1 : now.Year;
        var monthStart = new DateOnly(lastMonthYear, lastMonth, 1);

        _logger.LogInformation("Starting MonthlyFeatureAggregation for month starting {MonthStart}", monthStart);
        await ExecuteForMonthAsync(monthStart, CancellationToken.None);
    }

    public async Task ExecuteForMonthAsync(DateOnly monthStart, CancellationToken ct = default)
    {
        var daysInMonth = DateTime.DaysInMonth(monthStart.Year, monthStart.Month);
        var monthEnd = new DateOnly(monthStart.Year, monthStart.Month, daysInMonth);

        var users = await _context.DailyFocusScores
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.Date >= monthStart && d.Date <= monthEnd)
            .GroupBy(d => new { d.UserId, d.OrgId })
            .Select(g => new { g.Key.UserId, g.Key.OrgId, DaysWithData = g.Count() })
            .Where(u => u.DaysWithData >= 5)
            .ToListAsync(ct);

        _logger.LogInformation("Found {Count} users with sufficient data for month {MonthStart}", users.Count, monthStart);

        foreach (var user in users)
        {
            try
            {
                await AggregateUserMonthAsync(user.OrgId, user.UserId, monthStart, monthEnd, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error aggregating monthly features for user {UserId}", user.UserId);
            }
        }

        _logger.LogInformation("MonthlyFeatureAggregation completed for month {MonthStart}", monthStart);
    }

    private async Task AggregateUserMonthAsync(Guid orgId, Guid userId, DateOnly monthStart, DateOnly monthEnd, CancellationToken ct)
    {
        var dailyScores = await _context.DailyFocusScores
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(d => d.UserId == userId && d.Date >= monthStart && d.Date <= monthEnd)
            .ToListAsync(ct);

        if (dailyScores.Count < 5)
            return;

        var totalActiveSeconds = dailyScores.Sum(d => (d.TotalTrackedMs / 1000));
        var totalProductiveSeconds = dailyScores.Sum(d => d.ProductiveSeconds);
        var totalDistractionSeconds = dailyScores.Sum(d => d.DistractionSeconds);
        var totalNeutralSeconds = dailyScores.Sum(d => d.NeutralSeconds);
        var totalAllSeconds = totalProductiveSeconds + totalDistractionSeconds + totalNeutralSeconds;

        var monthly = FeatureMonthly.Create(
            id: Guid.NewGuid(),
            orgId: orgId,
            userId: userId,
            monthStart: monthStart,
            avgFocusScore: dailyScores.Average(d => (double)d.FocusScore),
            avgProductiveRatio: totalAllSeconds > 0 ? (double)totalProductiveSeconds / totalAllSeconds : 0,
            totalActiveHours: totalActiveSeconds / 3600.0);

        // Calculate trend vs previous month
        var prevMonth = monthStart.Month == 1 ? 12 : monthStart.Month - 1;
        var prevYear = monthStart.Month == 1 ? monthStart.Year - 1 : monthStart.Year;
        var prevMonthStart = new DateOnly(prevYear, prevMonth, 1);
        var prevMonthly = await _monthlyRepo.GetByUserAndMonthAsync(userId, prevMonthStart, ct);

        monthly.UpdateTrend(previousFocusScore: prevMonthly?.AvgFocusScore);

        await _monthlyRepo.UpsertAsync(monthly, ct);
    }
}
