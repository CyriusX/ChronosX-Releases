namespace TimeTrack.Backend.AI.Configuration;

public sealed class LiveInsightOptions
{
    public const string SectionName = "LiveInsight";

    public int IntervalMinutes { get; set; } = 10;
    public int DeduplicationWindowHours { get; set; } = 2;
    public double DistractionThreshold { get; set; } = 0.4;
    public int OverworkMinutesThreshold { get; set; } = 120;
    public int ContextSwitchHourlyThreshold { get; set; } = 20;
    public int FocusDropPointThreshold { get; set; } = 15;
    public int DeepFocusMinutesThreshold { get; set; } = 45;
    public int MaxAiCallsPerCycle { get; set; } = 2;
    public double TeamDistractionRatio { get; set; } = 0.3;
}
