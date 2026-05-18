namespace TimeTrack.Backend.AI.Interfaces;

public sealed class AppClassificationRequest
{
    public string ExeName { get; init; } = string.Empty;
    public List<string> SampleWindowTitles { get; init; } = [];
    public int AvgDailySeconds { get; init; }
    public int UsageDaysLast30 { get; init; }
    public Dictionary<string, double>? UsageHours { get; init; }
    public double? CorrelationWithFocusScore { get; init; }
    public List<string>? CoOccurringApps { get; init; }
    public List<UserUsageEntry>? PerUserBreakdown { get; init; }
    public List<FeedbackExample>? FewShotExamples { get; init; }
}

public sealed class FeedbackExample
{
    public string ExeName { get; init; } = string.Empty;
    public string CorrectCategory { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public sealed class UserUsageEntry
{
    public string UserName { get; init; } = string.Empty;
    public double Hours { get; init; }
}

public sealed class ClassificationResult
{
    public string Category { get; init; } = string.Empty;
    public string Subcategory { get; init; } = string.Empty;
    public double Confidence { get; init; }
    public string Reasoning { get; init; } = string.Empty;
}

public sealed class UserAiContext
{
    public string? Role { get; init; }
    public List<string> TopApps { get; init; } = [];
    public double? TeamAvgFocusScore { get; init; }
    public double? PreviousPeriodFocus { get; init; }
    public int TeamSize { get; init; }
    public string? OrgName { get; init; }
}

public sealed class WeeklyFeatureContext
{
    public string Period { get; init; } = string.Empty;
    public WeeklyFeatures Features { get; init; } = new();
    public List<PatternSummary> Patterns { get; init; } = [];
    public List<AnomalySummary> Anomalies { get; init; } = [];
    public string Language { get; init; } = "pt-BR";
    public string Tone { get; init; } = "profissional e encorajador";
    public int MaxSentences { get; init; } = 4;
    public UserAiContext? UserContext { get; init; }
}

public sealed class WeeklyFeatures
{
    public double TotalActiveHours { get; init; }
    public double AvgFocusScore { get; init; }
    public double AvgProductiveRatio { get; init; }
    public double? TrendFocusScore { get; init; }
    public double? TrendProductiveRatio { get; init; }
}

public sealed class PatternSummary
{
    public string Tag { get; init; } = string.Empty;
    public double Strength { get; init; }
}

public sealed class AnomalySummary
{
    public string Type { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
}

public sealed class UserPatternContext
{
    public string PatternTag { get; init; } = string.Empty;
    public double Strength { get; init; }
    public string Evidence { get; init; } = string.Empty;
    public string Language { get; init; } = "pt-BR";
    public UserAiContext? UserContext { get; init; }
}

public sealed class ReportsSuggestionContext
{
    public double FocusScore { get; init; }
    public string Trend { get; init; } = "stable";
    public double? BaselineFocus { get; init; }
    public double? FocusDiff { get; init; }
    public double AvgContextSwitches { get; init; }
    public List<PatternSummary> Patterns { get; init; } = [];
    public List<AnomalySummary> Anomalies { get; init; } = [];
    public string Language { get; init; } = "pt-BR";
    public UserAiContext? UserContext { get; init; }
    public List<string>? TopDistractions { get; init; }
}

public sealed class WeeklyEmailReportContext
{
    public string UserName { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
    public string Language { get; init; } = "pt-BR";

    public UserWeekMetrics CurrentWeek { get; init; } = new();
    public UserWeekMetrics? PreviousWeek { get; init; }

    public List<TeamMemberRanking> TeamRankings { get; init; } = [];
    public List<DifficultyItem> TopDifficulties { get; init; } = [];
    public List<UnproductiveDay> UnproductiveDays { get; init; } = [];
    public List<string> TopApps { get; init; } = [];

    public ReportPreferences Preferences { get; init; } = new();
}

public sealed class UserWeekMetrics
{
    public double TotalActiveHours { get; init; }
    public double AvgFocusScore { get; init; }
    public double ProductiveRatio { get; init; }
    public int ContextSwitches { get; init; }
    public int InterruptionCount { get; init; }
}

public sealed class TeamMemberRanking
{
    public string Name { get; init; } = string.Empty;
    public double FocusScore { get; init; }
    public double ProductiveRatio { get; init; }
    public int Position { get; init; }
}

public sealed class DifficultyItem
{
    public string AppName { get; init; } = string.Empty;
    public double Hours { get; init; }
    public string Category { get; init; } = string.Empty;
}

public sealed class UnproductiveDay
{
    public string DayName { get; init; } = string.Empty;
    public double FocusScore { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public sealed class ReportPreferences
{
    public bool IncludeTeamComparison { get; init; } = true;
    public bool IncludeDifficultyAnalysis { get; init; } = true;
    public bool IncludeWeekOverWeek { get; init; } = true;
    public bool IncludeUnproductiveDays { get; init; } = true;

    public static ReportPreferences Default => new();
}

public sealed class AlertContext
{
    public string AlertType { get; init; } = string.Empty;
    public string TargetName { get; init; } = string.Empty;
    public string Severity { get; init; } = "info";
    public Dictionary<string, object> Data { get; init; } = [];
    public string Language { get; init; } = "pt-BR";
    public UserAiContext? UserContext { get; init; }
}
