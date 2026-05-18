using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class ThresholdUpdateJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ThresholdUpdateJob> _logger;

    public ThresholdUpdateJob(
        TimeTrackDbContext context,
        IMemoryCache cache,
        ILogger<ThresholdUpdateJob> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting ThresholdUpdateJob");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var thirtyDaysAgo = today.AddDays(-30);

        var users = await _context.DailyFocusScores
            .IgnoreQueryFilters()
            .Where(d => d.Date >= thirtyDaysAgo)
            .GroupBy(d => d.UserId)
            .Where(g => g.Count() >= 14)
            .Select(g => new { UserId = g.Key })
            .ToListAsync();

        _logger.LogInformation("Computing thresholds for {Count} users", users.Count);

        var updated = 0;

        foreach (var user in users)
        {
            var scores = await _context.DailyFocusScores
                .IgnoreQueryFilters()
                .Where(d => d.UserId == user.UserId && d.Date >= thirtyDaysAgo)
                .ToListAsync();

            if (scores.Count < 14) continue;

            var focusValues = scores.Select(s => (double)s.FocusScore).ToList();
            var avgFocus = focusValues.Average();
            var stdDevFocus = StdDev(focusValues, avgFocus);

            var ratios = scores.Select(s =>
            {
                var total = s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds;
                return total > 0 ? (double)s.ProductiveSeconds / total : 0;
            }).ToList();
            var avgRatio = ratios.Average();
            var stdDevRatio = StdDev(ratios, avgRatio);

            var avgContextSwitches = scores.Average(s => s.ContextSwitchesCount);

            var activeSeconds = scores
                .Select(s => (double)(s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds))
                .ToList();
            var avgActiveHours = activeSeconds.Average() / 3600.0;

            var threshold = new UserThreshold
            {
                AvgFocusScore = Math.Round(avgFocus, 2),
                StdDevFocusScore = Math.Round(stdDevFocus, 2),
                FocusDropThreshold = Math.Round(avgFocus * 0.6, 2),
                AvgProductiveRatio = Math.Round(avgRatio, 4),
                StdDevProductiveRatio = Math.Round(stdDevRatio, 4),
                AvgContextSwitches = Math.Round(avgContextSwitches, 1),
                AvgActiveHours = Math.Round(avgActiveHours, 2),
                ComputedAt = DateTime.UtcNow,
            };

            _cache.Set(ThresholdCacheKey(user.UserId), threshold, TimeSpan.FromDays(8));
            updated++;
        }

        _logger.LogInformation("ThresholdUpdateJob completed. Updated: {Count}", updated);
    }

    public static string ThresholdCacheKey(Guid userId) => $"user_threshold:{userId}";

    private static double StdDev(List<double> values, double mean)
    {
        if (values.Count < 2) return 0;
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
}

public sealed class UserThreshold
{
    public double AvgFocusScore { get; init; }
    public double StdDevFocusScore { get; init; }
    public double FocusDropThreshold { get; init; }
    public double AvgProductiveRatio { get; init; }
    public double StdDevProductiveRatio { get; init; }
    public double AvgContextSwitches { get; init; }
    public double AvgActiveHours { get; init; }
    public DateTime ComputedAt { get; init; }
}
