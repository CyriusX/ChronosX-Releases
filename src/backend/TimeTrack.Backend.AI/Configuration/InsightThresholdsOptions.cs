namespace TimeTrack.Backend.AI.Configuration;

public sealed class InsightThresholdsOptions
{
    public const string SectionName = "InsightThresholds";

    public double TrendImprovingThreshold { get; set; } = 5.0;
    public double TrendDecliningThreshold { get; set; } = -5.0;
    public int LiveDeepFocusMinutes { get; set; } = 25;
    public int LiveContextSwitchThreshold { get; set; } = 15;
    public double AutoAcceptConfidence { get; set; } = 0.90;
    public int DefaultPageSize { get; set; } = 20;
}
