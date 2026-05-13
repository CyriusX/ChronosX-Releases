namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Computed live productivity metrics from activity sessions.
/// </summary>
public sealed record LiveMetrics
{
    public long ProductiveMs { get; init; }
    public long DistractionMs { get; init; }
    public long NeutralMs { get; init; }
    public long TotalMs { get; init; }
    public int ContextSwitches { get; init; }
    public int RecentContextSwitches { get; init; }
    public long LongestFocusBlockMs { get; init; }
    public string? TopDistractionApp { get; init; }
    public long TopDistractionMs { get; init; }
}
