using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.GetLocalDashboard;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Handles getting today's summary
/// </summary>
public sealed class GetTodaySummaryQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTodaySummary";

    private readonly GetLocalDashboardUseCase _getDashboard;
    private readonly IBackendReportsClient _reportsClient;
    private readonly ILogger<GetTodaySummaryQueryHandler> _logger;

    // Internal apps excluded from dashboard totals (same as GetLocalDashboardUseCase)
    private static readonly HashSet<string> InternalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimeTrack.DesktopHost",
        "Microsoft Edge WebView2",
        "Microsoft® Windows® Operating System",
        "Sistema operacional Microsoft® Windows®"
    };

    public GetTodaySummaryQueryHandler(
        GetLocalDashboardUseCase getDashboard,
        IBackendReportsClient reportsClient,
        ILogger<GetTodaySummaryQueryHandler> logger)
    {
        _getDashboard = getDashboard;
        _reportsClient = reportsClient;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var targetDate = ExtractDateOrToday(request);
            var dashboard = await _getDashboard.ExecuteAsync(targetDate, ct);

            // All durations are in seconds for sub-minute precision.
            // The UI formats them via formatDuration(seconds).
            var summary = new
            {
                totalDuration  = (long)dashboard.TotalWorkTime.TotalSeconds,
                // productiveTime = only time spent in apps classified as "productive"
                productiveTime = (long)TimeSpan.FromMilliseconds(dashboard.FocusTimeMs).TotalSeconds,
                idleTime       = (long)dashboard.TotalIdleTime.TotalSeconds,
                focusTime      = (long)TimeSpan.FromMilliseconds(dashboard.FocusTimeMs).TotalSeconds,
                focusScore     = dashboard.FocusScore,
                sessionsCount  = dashboard.SessionCount,
                topProjects    = Array.Empty<object>(),

                // "productivity" matches ApplicationSummary.productivity in TypeScript
                topApplications = dashboard.TopApplications.Select(a => new
                {
                    name        = a.DisplayName,
                    duration    = (long)a.TotalTime.TotalSeconds,
                    percentage  = a.Percentage,
                    productivity = a.ProductivityCategory,
                    subcategory = a.Subcategory
                }).ToArray(),

                // Group by subcategory for category cards.
                // "browser_general" is too broad — merge it into "Outros" (Other).
                // "unknown" falls back to productivity level.
                categories = dashboard.TopApplications
                    .GroupBy(a =>
                    {
                        var sub = a.Subcategory;
                        if (string.IsNullOrEmpty(sub) || sub == "unknown")
                            return a.ProductivityCategory;
                        if (sub == "browser_general")
                            return "other";
                        return sub;
                    })
                    .Select(g => new
                    {
                        name        = FormatCategoryName(g.Key),
                        duration    = (long)g.Sum(a => a.TotalTime.TotalSeconds),
                        percentage  = g.Sum(a => a.Percentage),
                        color       = GetCategoryColor(g.Key),
                        productivity = g.First().ProductivityCategory
                    })
                    .OrderByDescending(c => c.duration)
                    .ToArray(),

                // Top apps grouped by executable (browser tabs merged into parent app name)
                topAppsByExe = dashboard.TopAppsByExe.Select(a => new
                {
                    name        = a.DisplayName,
                    duration    = (long)a.TotalTime.TotalSeconds,
                    percentage  = a.Percentage,
                    productivity = a.ProductivityCategory,
                    subcategory = a.Subcategory
                }).ToArray(),

                weeklyHistory = await BuildWeeklyHistoryAsync(targetDate, ct)
            };

            return SuccessResponse(request.RequestId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today summary");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    /// <summary>
    /// Builds weekly history: today from local SQLite (real-time), past days from backend API (cloud).
    /// Falls back to local SQLite if the backend is unreachable.
    /// </summary>
    private async Task<object[]> BuildWeeklyHistoryAsync(DateTime centerDate, CancellationToken ct)
    {
        var today = centerDate;
        var history = new List<object>();
        var dayNames = new[] { "Dom", "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb" };

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            double hours = 0;

            if (i == 0)
            {
                // TODAY: use local SQLite (fast, real-time, includes in-memory session)
                try
                {
                    var dayDashboard = await _getDashboard.ExecuteAsync(date, ct);
                    hours = dayDashboard.TotalWorkTime.TotalHours;
                }
                catch { /* show 0 */ }
            }
            else
            {
                // PAST DAYS: fetch from backend API (authoritative cloud data)
                try
                {
                    // Small delay between API calls to avoid rate limiting (429)
                    if (i < 6) await Task.Delay(200, ct);

                    var report = await _reportsClient.GetDailySummaryAsync(date, ct);
                    if (report != null)
                    {
                        // Filter out internal apps (same filter as local dashboard)
                        var filteredSeconds = report.Apps
                            .Where(a => !InternalApps.Contains(a.DisplayName))
                            .Sum(a => a.TotalSeconds);
                        hours = filteredSeconds / 3600.0;
                    }
                    else
                    {
                        // Backend unavailable — fall back to local SQLite
                        var dayDashboard = await _getDashboard.ExecuteAsync(date, ct);
                        hours = dayDashboard.TotalWorkTime.TotalHours;
                    }
                }
                catch
                {
                    // Both sources failed — show 0
                }
            }

            history.Add(new
            {
                date = date.ToString("yyyy-MM-dd"),
                dayName = dayNames[(int)date.DayOfWeek],
                hours = Math.Round(hours, 2),
                isToday = i == 0
            });
        }

        return history.ToArray();
    }

    /// <summary>
    /// Formats a raw subcategory key into a user-friendly name.
    /// "productivity_tools" → "Productivity Tools", "social_media" → "Social Media"
    /// </summary>
    private static string FormatCategoryName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "Outros";

        // Known friendly names
        var friendlyNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["development"]        = "Development",
            ["design"]             = "Design",
            ["productivity_tools"] = "Productivity",
            ["communication"]      = "Communication",
            ["meetings"]           = "Meetings",
            ["entertainment"]      = "Entertainment",
            ["social_media"]       = "Social Media",
            ["browser_general"]    = "Browsing",
            ["other"]              = "Outros",
            ["productive"]         = "Productive",
            ["neutral"]            = "Neutral",
            ["distraction"]        = "Distraction",
        };

        if (friendlyNames.TryGetValue(key, out var friendly))
            return friendly;

        // Fallback: replace underscores, capitalize each word
        return string.Join(' ', key.Split('_').Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
    }

    private static string GetCategoryColor(string key) =>
        key?.ToLowerInvariant() switch
        {
            "development"        => "#38bdf8",   // sky blue
            "design"             => "#a78bfa",   // violet
            "productivity_tools" => "#34d399",   // emerald
            "productivity"       => "#34d399",
            "communication"      => "#fb923c",   // orange
            "meetings"           => "#22d3ee",   // cyan
            "entertainment"      => "#f87171",   // red
            "social_media"       => "#f472b6",   // pink
            "other"              => "#94a3b8",   // slate
            "productive"         => "#4ade80",   // green
            "neutral"            => "#fbbf24",   // amber
            "distraction"        => "#ef4444",   // red-600
            _                    => "#94a3b8"
        };
}
