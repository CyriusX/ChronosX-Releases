namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Thresholds for live insight detection, passed as primitives
/// to avoid domain dependency on configuration infrastructure.
/// </summary>
public sealed record LiveInsightThresholds
{
    public double DistractionThreshold { get; init; } = 0.4;
    public int OverworkMinutesThreshold { get; init; } = 120;
    public int DeepFocusMinutesThreshold { get; init; } = 45;
}
