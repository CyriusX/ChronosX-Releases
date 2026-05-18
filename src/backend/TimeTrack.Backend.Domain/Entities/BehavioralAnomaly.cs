using System.Text.Json;

namespace TimeTrack.Backend.Domain.Entities;

public sealed class BehavioralAnomaly
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid OrgId { get; private set; }
    public string AnomalyType { get; private set; } = string.Empty;
    public string Severity { get; private set; } = "info";
    public DateOnly DetectedAt { get; private set; }
    public JsonElement Evidence { get; private set; }
    public double? BaselineValue { get; private set; }
    public double? ActualValue { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private BehavioralAnomaly() { }

    public static BehavioralAnomaly Create(
        Guid userId,
        Guid orgId,
        string anomalyType,
        string severity,
        DateOnly detectedAt,
        JsonElement evidence,
        double? baselineValue = null,
        double? actualValue = null)
    {
        return new BehavioralAnomaly
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OrgId = orgId,
            AnomalyType = anomalyType,
            Severity = severity,
            DetectedAt = detectedAt,
            Evidence = evidence,
            BaselineValue = baselineValue,
            ActualValue = actualValue,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Detects when a user's productivity ratio falls significantly below their baseline
    /// using statistical analysis (standard deviation).
    /// </summary>
    public static BehavioralAnomaly? TryCreateProductivityDrop(
        Guid userId,
        Guid orgId,
        DateOnly detectedAt,
        List<DailyFocusScore> baseline,
        DailyFocusScore yesterday,
        double sigmaMultiplier,
        int minBaselineDays)
    {
        if (baseline.Count < minBaselineDays) return null;

        var ratios = baseline
            .Select(s =>
            {
                var total = s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds;
                return total > 0 ? (double)s.ProductiveSeconds / total : 0;
            })
            .ToList();

        var avg = ratios.Average();
        var stddev = StdDev(ratios, avg);

        var yesterdayTotal = yesterday.ProductiveSeconds + yesterday.DistractionSeconds + yesterday.NeutralSeconds;
        var yesterdayRatio = yesterdayTotal > 0 ? (double)yesterday.ProductiveSeconds / yesterdayTotal : 0;

        var threshold = avg - sigmaMultiplier * stddev;
        if (!(yesterdayRatio < threshold)) return null;

        var evidence = JsonSerializer.SerializeToElement(new
        {
            baselineAvg = Math.Round(avg, 3),
            baselineStdDev = Math.Round(stddev, 3),
            threshold = Math.Round(threshold, 3),
            actual = Math.Round(yesterdayRatio, 3),
        });

        return Create(
            userId, orgId, "productivity_drop", "warning", detectedAt,
            evidence, Math.Round(avg, 3), Math.Round(yesterdayRatio, 3));
    }

    /// <summary>
    /// Detects days with very low total tracked time (excluding weekends).
    /// </summary>
    public static BehavioralAnomaly? TryCreateAbsentWorkday(
        Guid userId,
        Guid orgId,
        DateOnly detectedAt,
        DailyFocusScore yesterday,
        int minimumWorkdaySeconds)
    {
        if (yesterday.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return null;

        var totalSeconds = yesterday.ProductiveSeconds + yesterday.DistractionSeconds + yesterday.NeutralSeconds;
        if (totalSeconds >= minimumWorkdaySeconds) return null;

        var evidence = JsonSerializer.SerializeToElement(new
        {
            totalTrackedSeconds = totalSeconds,
            dayOfWeek = yesterday.Date.DayOfWeek.ToString(),
        });

        return Create(
            userId, orgId, "absent_workday", "info", detectedAt,
            evidence, minimumWorkdaySeconds, totalSeconds);
    }

    /// <summary>
    /// Detects exceptionally long work days compared to the user's baseline average.
    /// </summary>
    public static BehavioralAnomaly? TryCreateLongDay(
        Guid userId,
        Guid orgId,
        DateOnly detectedAt,
        DailyFocusScore yesterday,
        List<DailyFocusScore> baseline,
        double longDayMultiplier)
    {
        var dailyTotals = baseline
            .Select(s => (double)(s.ProductiveSeconds + s.DistractionSeconds + s.NeutralSeconds))
            .ToList();

        var avg = dailyTotals.Average();
        var yesterdayTotal = (double)(yesterday.ProductiveSeconds + yesterday.DistractionSeconds + yesterday.NeutralSeconds);

        if (!(yesterdayTotal > avg * longDayMultiplier)) return null;

        var evidence = JsonSerializer.SerializeToElement(new
        {
            baselineAvgSeconds = (int)avg,
            actualSeconds = (int)yesterdayTotal,
            multiplier = Math.Round(yesterdayTotal / avg, 2),
        });

        return Create(
            userId, orgId, "exceptionally_long_day", "info", detectedAt,
            evidence, Math.Round(avg / 3600, 1), Math.Round(yesterdayTotal / 3600, 1));
    }

    /// <summary>
    /// Detects 3 consecutive days with focus scores significantly below the personal baseline.
    /// </summary>
    public static BehavioralAnomaly? TryCreateFocusCollapse(
        Guid userId,
        Guid orgId,
        DateOnly detectedAt,
        List<DailyFocusScore> recentScores,
        List<DailyFocusScore> baseline,
        double focusCollapseRatio)
    {
        var avgBaseline = baseline.Average(s => s.FocusScore);
        var threshold = avgBaseline * focusCollapseRatio;

        var last3Days = recentScores
            .OrderByDescending(s => s.Date)
            .Take(3)
            .ToList();

        if (last3Days.Count < 3) return null;
        if (!last3Days.All(s => s.FocusScore < threshold)) return null;

        var evidence = JsonSerializer.SerializeToElement(new
        {
            baselineAvgFocus = Math.Round(avgBaseline, 1),
            threshold = Math.Round(threshold, 1),
            recentScores = last3Days.Select(s => new { s.Date, s.FocusScore }),
        });

        return Create(
            userId, orgId, "focus_collapse", "alert", detectedAt,
            evidence, Math.Round(avgBaseline, 1), Math.Round(last3Days.Average(s => s.FocusScore), 1));
    }

    private static double StdDev(List<double> values, double mean)
    {
        if (values.Count < 2) return 0;
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (values.Count - 1));
    }
}
