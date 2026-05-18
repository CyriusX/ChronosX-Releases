namespace TimeTrack.Backend.Application.Insights.DTOs;

public sealed class DailyInsightResponse
{
    public bool HasData { get; init; }
    public string Date { get; init; } = string.Empty;
    public double? FocusScore { get; init; }
    public int? ProductiveSeconds { get; init; }
    public int? DistractionSeconds { get; init; }
    public int? NeutralSeconds { get; init; }
    public double? ProductivityRatio { get; init; }
    public int? ContextSwitches { get; init; }
    public int? LongestFocusMinutes { get; init; }
    public DailyInsightComparison? Comparison { get; init; }
    public string? Insight { get; init; }
    public string? Suggestion { get; init; }
    public List<PatternItem> Patterns { get; init; } = [];
    public List<AnomalyItem> Anomalies { get; init; } = [];
    public List<AlertItem> Alerts { get; init; } = [];
}

public sealed class DailyInsightComparison
{
    public double? PersonalAvgFocus { get; init; }
    public double? FocusDiff { get; init; }
    public double? FocusDiffPercent { get; init; }
    public string? Trend { get; init; }
    public int BaselineDays { get; init; }
}

public sealed class WeeklyNarrativeResponse
{
    public string? Narrative { get; init; }
    public string WeekStart { get; init; } = string.Empty;
    public string? ModelVersion { get; init; }
    public bool? WasReviewed { get; init; }
    public string? ReviewOutcome { get; init; }
}

public sealed class PatternListResponse
{
    public List<PatternItem> Patterns { get; init; } = [];
}

public sealed class PatternItem
{
    public Guid Id { get; init; }
    public string PatternTag { get; init; } = string.Empty;
    public double Strength { get; init; }
    public string? Description { get; init; }
    public DateOnly? DetectedAt { get; init; }
}

public sealed class FocusTrendResponse
{
    public List<FocusTrendDataPoint> Data { get; init; } = [];
}

public sealed class FocusTrendDataPoint
{
    public DateOnly WeekStart { get; init; }
    public double AvgFocusScore { get; init; }
    public double AvgProductiveRatio { get; init; }
    public double TotalActiveHours { get; init; }
    public double? TrendFocusScore { get; init; }
}

public sealed class BenchmarkResponse
{
    public bool HasData { get; init; }
    public BenchmarkUser? User { get; init; }
    public BenchmarkTeam? Team { get; init; }
    public double? FocusDiff { get; init; }
    public double? FocusDiffPercent { get; init; }
}

public sealed class BenchmarkUser
{
    public double AvgFocusScore { get; init; }
    public double AvgProductiveRatio { get; init; }
}

public sealed class BenchmarkTeam
{
    public double AvgFocusScore { get; init; }
    public double AvgProductiveRatio { get; init; }
    public int UserCount { get; init; }
}

public sealed class NarrativeHistoryResponse
{
    public List<NarrativeHistoryItem> Narratives { get; init; } = [];
}

public sealed class NarrativeHistoryItem
{
    public Guid Id { get; init; }
    public string? Narrative { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public bool WasReviewed { get; init; }
    public string? ReviewOutcome { get; init; }
}

public sealed class LiveInsightResponse
{
    public bool HasData { get; init; }
    public long ProductiveSeconds { get; init; }
    public long DistractionSeconds { get; init; }
    public long NeutralSeconds { get; init; }
    public int ContextSwitches { get; init; }
    public long LongestFocusMinutes { get; init; }
    public List<LiveInsightItem> Insights { get; init; } = [];
    public AlertItem? LatestAlert { get; init; }
}

public sealed class LiveInsightItem
{
    public string Type { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class TeamLiveInsightResponse
{
    public List<TeamMemberLiveInsight> Members { get; init; } = [];
    public AlertItem? OrgAlert { get; init; }
}

public sealed class TeamMemberLiveInsight
{
    public Guid UserId { get; init; }
    public string UserName { get; init; } = string.Empty;
    public double FocusEstimate { get; init; }
    public long ActiveSeconds { get; init; }
    public AlertItem? LatestAlert { get; init; }
}

public sealed class ReportsInsightsResponse
{
    public bool HasData { get; init; }
    public bool IsTeamView { get; init; }
    public DateRangeInfo DateRange { get; init; } = new();
    public double? FocusScore { get; init; }
    public string? Insight { get; init; }
    public string? Suggestion { get; init; }
    public ReportsSummary? Summary { get; init; }
    public List<string> TopApps { get; init; } = [];
    public List<string> TopDistractions { get; init; } = [];
    public ReportsComparison? Comparison { get; init; }
    public List<PatternItem> Patterns { get; init; } = [];
    public List<AnomalyItem> Anomalies { get; init; } = [];
    public List<AlertItem> Alerts { get; init; } = [];
    public BenchmarkResponse? Benchmark { get; init; }
    public TeamInsightsSummary? TeamSummary { get; init; }
}

public sealed class DateRangeInfo
{
    public string StartDate { get; init; } = string.Empty;
    public string EndDate { get; init; } = string.Empty;
}

public sealed class ReportsSummary
{
    public double TotalTrackedHours { get; init; }
    public double ProductiveHours { get; init; }
    public double DistractionHours { get; init; }
    public double FocusScore { get; init; }
    public double ContextSwitchesPerDay { get; init; }
    public string? Trend { get; init; }
}

public sealed class ReportsComparison
{
    public double? PersonalAvgFocus { get; init; }
    public double? FocusDiff { get; init; }
    public string? Trend { get; init; }
    public int BaselineDays { get; init; }
}

public sealed class TeamInsightsSummary
{
    public double AvgFocusScore { get; init; }
    public int MemberCount { get; init; }
    public List<TeamPatternItem> TopPatterns { get; init; } = [];
    public int TotalAnomalies { get; init; }
    public int TotalAlerts { get; init; }
}

public sealed class TeamPatternItem
{
    public string PatternTag { get; init; } = string.Empty;
    public double Strength { get; init; }
    public string? Description { get; init; }
}

public sealed class AnomalyItem
{
    public string AnomalyType { get; init; } = string.Empty;
    public string Severity { get; init; } = string.Empty;
}

public sealed class AlertItem
{
    public Guid Id { get; init; }
    public string AlertType { get; init; } = string.Empty;
    public string? Message { get; init; }
    public string? Severity { get; init; }
    public string? ActionType { get; init; }
    public DateTime? CreatedAt { get; init; }
}
