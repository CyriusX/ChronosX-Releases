namespace TimeTrack.Backend.Application.Reports.DTOs;

public sealed class WeeklyReportScheduleRequest
{
    public int DayOfWeek { get; init; }
    public string TimeOfDay { get; init; } = "09:00";
    public bool IsEnabled { get; init; } = true;
    public ReportPreferencesRequest? Preferences { get; init; }
}

public sealed class ReportPreferencesRequest
{
    public bool IncludeTeamComparison { get; init; } = true;
    public bool IncludeDifficultyAnalysis { get; init; } = true;
    public bool IncludeWeekOverWeek { get; init; } = true;
    public bool IncludeUnproductiveDays { get; init; } = true;
}

public sealed class WeeklyReportScheduleResponse
{
    public Guid Id { get; init; }
    public int DayOfWeek { get; init; }
    public string DayName { get; init; } = string.Empty;
    public string TimeOfDay { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public ReportPreferencesResponse Preferences { get; init; } = new();
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class ReportPreferencesResponse
{
    public bool IncludeTeamComparison { get; init; } = true;
    public bool IncludeDifficultyAnalysis { get; init; } = true;
    public bool IncludeWeekOverWeek { get; init; } = true;
    public bool IncludeUnproductiveDays { get; init; } = true;
}

public sealed class WeeklyReportPreviewResponse
{
    public string HtmlReport { get; init; } = string.Empty;
    public string Period { get; init; } = string.Empty;
}
