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
    public string DisplayName { get; init; } = string.Empty;
    public long TotalSeconds { get; init; }
    public int SessionCount { get; init; }
}
