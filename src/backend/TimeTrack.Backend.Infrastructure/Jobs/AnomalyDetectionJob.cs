using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Backend.AI.Configuration;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class AnomalyDetectionJob
{
    private readonly TimeTrackDbContext _context;
    private readonly ILogger<AnomalyDetectionJob> _logger;
    private readonly AnomalyDetectionOptions _options;

    public AnomalyDetectionJob(
        TimeTrackDbContext context,
        ILogger<AnomalyDetectionJob> logger,
        IOptions<AnomalyDetectionOptions> options)
    {
        _context = context;
        _logger = logger;
        _options = options.Value;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting AnomalyDetectionJob");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        var usersWithBaseline = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date >= thirtyDaysAgo)
            .GroupBy(d => d.UserId)
            .Where(g => g.Count() >= _options.MinimumBaselineDays)
            .Select(g => new { UserId = g.Key, OrgId = g.First().OrgId, DayCount = g.Count() })
            .ToListAsync();

        _logger.LogInformation("Found {Count} users with sufficient baseline for anomaly detection", usersWithBaseline.Count);

        var qualifyingUserIds = usersWithBaseline.Select(u => u.UserId).ToHashSet();

        var allScores = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => qualifyingUserIds.Contains(d.UserId) && d.Date >= thirtyDaysAgo)
            .OrderBy(d => d.UserId).ThenBy(d => d.Date)
            .ToListAsync();

        var scoresByUser = allScores
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var anomaliesDetected = 0;

        foreach (var user in usersWithBaseline)
        {
            if (!scoresByUser.TryGetValue(user.UserId, out var thirtyDayScores))
                continue;

            var yesterdayScore = thirtyDayScores.FirstOrDefault(s => s.Date == today.AddDays(-1));
            if (yesterdayScore == null) continue;

            var baseline = thirtyDayScores.Where(s => s.Date < today.AddDays(-1)).ToList();
            if (baseline.Count < _options.MinimumBaselineDays) continue;

            AddIfNotNull(BehavioralAnomaly.TryCreateProductivityDrop(
                user.UserId, user.OrgId, today, baseline, yesterdayScore,
                _options.AnomalySigmaMultiplier, _options.MinimumBaselineDays));

            AddIfNotNull(BehavioralAnomaly.TryCreateAbsentWorkday(
                user.UserId, user.OrgId, today, yesterdayScore,
                _options.MinimumWorkdaySeconds));

            AddIfNotNull(BehavioralAnomaly.TryCreateLongDay(
                user.UserId, user.OrgId, today, yesterdayScore, baseline,
                _options.LongDayMultiplier));

            AddIfNotNull(BehavioralAnomaly.TryCreateFocusCollapse(
                user.UserId, user.OrgId, today, thirtyDayScores, baseline,
                _options.FocusCollapseRatio));
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("AnomalyDetectionJob completed. Anomalies detected: {Count}", anomaliesDetected);

        void AddIfNotNull(BehavioralAnomaly? anomaly)
        {
            if (anomaly == null) return;
            _context.BehavioralAnomalies.Add(anomaly);
            anomaliesDetected++;
        }
    }
}
