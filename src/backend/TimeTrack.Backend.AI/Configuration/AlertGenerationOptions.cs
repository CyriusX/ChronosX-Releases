namespace TimeTrack.Backend.AI.Configuration;

public sealed class AlertGenerationOptions
{
    public const string SectionName = "AlertGeneration";

    public int OverworkThresholdSeconds { get; set; } = 10800;
    public double FocusDropMultiplier { get; set; } = 0.6;
    public int ContextSwitchThreshold { get; set; } = 30;
    public double TeamHighSwitchingRatio { get; set; } = 0.3;
    public int MinimumTeamSize { get; set; } = 3;
}
