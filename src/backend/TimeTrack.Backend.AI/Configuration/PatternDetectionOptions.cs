namespace TimeTrack.Backend.AI.Configuration;

public sealed class PatternDetectionOptions
{
    public const string SectionName = "PatternDetection";

    public int StalePatternDays { get; set; } = 30;
    public int MinimumDataDays { get; set; } = 3;
    public double MorningProductiveRatioThreshold { get; set; } = 0.7;
    public double AfternoonFocusDropMultiplier { get; set; } = 0.5;
    public int ContextSwitchThreshold { get; set; } = 30;
    public int DeepWorkSecondsThreshold { get; set; } = 5400;
    public double DistractionRatioThreshold { get; set; } = 0.3;
}
