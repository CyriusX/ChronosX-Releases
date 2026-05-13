namespace TimeTrack.Backend.Domain.Entities;

public sealed class FeatureMonthly
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly MonthStart { get; private set; }
    public double AvgFocusScore { get; private set; }
    public double AvgProductiveRatio { get; private set; }
    public double TotalActiveHours { get; private set; }
    public double? TrendFocusScore { get; private set; }
    public DateTimeOffset ComputedAt { get; private set; }

    public User? User { get; private set; }

    private FeatureMonthly() { }

    public static FeatureMonthly Create(
        Guid id,
        Guid orgId,
        Guid userId,
        DateOnly monthStart,
        double avgFocusScore,
        double avgProductiveRatio,
        double totalActiveHours,
        double? trendFocusScore = null)
    {
        return new FeatureMonthly
        {
            Id = id,
            OrgId = orgId,
            UserId = userId,
            MonthStart = monthStart,
            AvgFocusScore = avgFocusScore,
            AvgProductiveRatio = avgProductiveRatio,
            TotalActiveHours = totalActiveHours,
            TrendFocusScore = trendFocusScore,
            ComputedAt = DateTimeOffset.UtcNow
        };
    }

    public void UpdateTrend(double? previousFocusScore)
    {
        TrendFocusScore = previousFocusScore.HasValue
            ? AvgFocusScore - previousFocusScore.Value
            : null;
        ComputedAt = DateTimeOffset.UtcNow;
    }
}
