using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// No-op implementation of IBackendReportsClient for local/test mode (no backend).
/// Always returns null — the UI will fall back to local SQLite data.
/// </summary>
public sealed class NullBackendReportsClient : IBackendReportsClient
{
    public Task<DailyReportResult?> GetDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default)
        => Task.FromResult<DailyReportResult?>(null);

    public Task<DailyActivitiesResult?> GetDailyActivitiesAsync(DateTime date, CancellationToken cancellationToken = default)
        => Task.FromResult<DailyActivitiesResult?>(null);
}
