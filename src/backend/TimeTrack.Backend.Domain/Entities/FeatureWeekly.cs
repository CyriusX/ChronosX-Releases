namespace TimeTrack.Backend.Domain.Entities;

public sealed class FeatureWeekly
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly WeekStart { get; private set; }
    public double AvgFocusScore { get; private set; }
    public double AvgProductiveRatio { get; private set; }
    public double TotalActiveHours { get; private set; }
    public double AvgContextSwitches { get; private set; }
    public double AvgInterruptionCount { get; private set; }
    public double? TrendFocusScore { get; private set; }
    public double? TrendProductiveRatio { get; private set; }
    public DateTimeOffset ComputedAt { get; private set; }

    public User? User { get; private set; }

    private FeatureWeekly() { }

    public static FeatureWeekly Create(
        Guid id,
        Guid orgId,
        Guid userId,
        DateOnly weekStart,
        double avgFocusScore,
        double avgProductiveRatio,
        double totalActiveHours,
        double avgContextSwitches,
        double avgInterruptionCount,
        double? trendFocusScore = null,
        double? trendProductiveRatio = null)
    {
        return new FeatureWeekly
        {
            Id = id,
            OrgId = orgId,
            UserId = userId,
            WeekStart = weekStart,
            AvgFocusScore = avgFocusScore,
            AvgProductiveRatio = avgProductiveRatio,
            TotalActiveHours = totalActiveHours,
            AvgContextSwitches = avgContextSwitches,
            AvgInterruptionCount = avgInterruptionCount,
            TrendFocusScore = trendFocusScore,
            TrendProductiveRatio = trendProductiveRatio,
            ComputedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateTrends(double? previousFocusScore, double? previousProductiveRatio)
    {
        TrendFocusScore = previousFocusScore.HasValue
            ? AvgFocusScore - previousFocusScore.Value
            : null;
        TrendProductiveRatio = previousProductiveRatio.HasValue
            ? AvgProductiveRatio - previousProductiveRatio.Value
            : null;
        ComputedAt = DateTimeOffset.UtcNow;
    }
}
