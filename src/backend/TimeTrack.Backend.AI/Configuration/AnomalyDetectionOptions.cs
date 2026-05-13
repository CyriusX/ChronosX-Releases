namespace TimeTrack.Backend.AI.Configuration;

public sealed class AnomalyDetectionOptions
{
    public const string SectionName = "AnomalyDetection";

    public int MinimumBaselineDays { get; set; } = 14;
    public double AnomalySigmaMultiplier { get; set; } = 2.0;
    public int MinimumWorkdaySeconds { get; set; } = 3600;
    public double LongDayMultiplier { get; set; } = 1.8;
    public double FocusCollapseRatio { get; set; } = 0.5;
}
