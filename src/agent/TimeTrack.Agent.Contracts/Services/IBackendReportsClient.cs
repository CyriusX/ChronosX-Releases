namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Client for fetching report data from the backend API.
/// Used to load past days' data for the weekly history chart,
/// since local SQLite only retains today's data.
/// </summary>
public interface IBackendReportsClient
{
    /// <summary>
    /// Fetches daily summary from backend GET /api/v1/reports/daily?date={date}.
    /// Returns per-app breakdown so the caller can filter internal apps.
    /// </summary>
    Task<DailyReportResult?> GetDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches individual activity sessions from backend GET /api/v1/reports/activities?date={date}.
    /// Returns session-level data (processName, windowTitle, startedAt, endedAt) for timeline display.
    /// </summary>
    Task<DailyActivitiesResult?> GetDailyActivitiesAsync(DateTime date, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from the backend daily report endpoint
/// </summary>
public sealed class DailyReportResult
{
    public long TotalActiveSeconds { get; init; }
    public long TotalIdleSeconds { get; init; }
    public List<DailyReportApp> Apps { get; init; } = [];
}

/// <summary>
/// Per-app breakdown from the daily report
/// </summary>
public sealed class DailyReportApp
{
    public string ProcessName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public int SessionCount { get; init; }
    public string? AppCategory { get; init; }
    /// <summary>
    /// Resolved productivity from cloud DB: "productive", "neutral", "distraction"
    /// </summary>
    public string? Productivity { get; init; }
}

/// <summary>
/// Result from the backend daily activities endpoint (session-level data)
/// </summary>
public sealed class DailyActivitiesResult
{
    public List<DailyActivitySession> Sessions { get; init; } = [];
    public List<DailyIdlePeriod> IdlePeriods { get; init; } = [];
}

/// <summary>
/// Individual activity session from the cloud
/// </summary>
public sealed class DailyActivitySession
{
    public string ProcessName { get; init; } = string.Empty;
    public string? WindowTitle { get; init; }
    public string? AppCategory { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public int DurationSeconds { get; init; }
}

public sealed class DailyIdlePeriod
{
    public Guid Id { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime EndedAt { get; init; }
    public int DurationSeconds { get; init; }
    public string? ReasonCode { get; init; }
    public string? Note { get; init; }
    public DateTime? SubmittedAtUtc { get; init; }
}
