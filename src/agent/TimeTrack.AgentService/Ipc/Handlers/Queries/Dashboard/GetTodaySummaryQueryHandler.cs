using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.GetLocalDashboard;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Handles getting summary data for a given date.
/// Today: uses local SQLite (fast, real-time).
/// Past days: fetches from backend cloud API (authoritative source), falls back to local SQLite.
/// </summary>
public sealed class GetTodaySummaryQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTodaySummary";

    private readonly GetLocalDashboardUseCase _getDashboard;
    private readonly IBackendReportsClient _reportsClient;
    private readonly ILogger<GetTodaySummaryQueryHandler> _logger;

    // Internal apps excluded from dashboard totals (must match all other views)
    private static readonly HashSet<string> InternalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimeTrack.DesktopHost",
        "ChronosX TimeTrack",
        "TimeTrack",
        "TimeTrack.MacOSAgentService",
        "Microsoft Edge WebView2",
        "Microsoft® Windows® Operating System",
        "Sistema operacional Microsoft® Windows®",
        "Tracking Stopped",
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

            var isToday = targetDate.Date == DateTime.Today;

            if (isToday)
            {
                return await BuildFromLocalDashboard(request.RequestId, targetDate, ct);
            }

            return await BuildFromBackendApi(request.RequestId, targetDate, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today summary");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    /// <summary>
    /// Builds summary from local SQLite (today's data, real-time).
    /// </summary>
    private async Task<IpcResponse> BuildFromLocalDashboard(int requestId, DateTime targetDate, CancellationToken ct)
    {
        var dashboard = await _getDashboard.ExecuteAsync(targetDate, ct);

        var totalDurationSeconds = (long)dashboard.TotalWorkTime.TotalSeconds;
        _logger.LogInformation(
            "[TodaySummary] LOCAL totalDuration={TotalDuration}s ({Hours}h {Minutes}m {Seconds}s), sessions={Sessions}",
            totalDurationSeconds,
            (int)(dashboard.TotalWorkTime.TotalHours),
            dashboard.TotalWorkTime.Minutes,
            dashboard.TotalWorkTime.Seconds,
            dashboard.SessionCount);

        var summary = new
        {
            totalDuration  = totalDurationSeconds,
            productiveTime = (long)TimeSpan.FromMilliseconds(dashboard.FocusTimeMs).TotalSeconds,
            idleTime       = (long)dashboard.TotalIdleTime.TotalSeconds,
            focusTime      = (long)TimeSpan.FromMilliseconds(dashboard.FocusTimeMs).TotalSeconds,
            focusScore     = dashboard.FocusScore,
            sessionsCount  = dashboard.SessionCount,
            topProjects    = Array.Empty<object>(),

            topApplications = dashboard.TopApplications.Select(a => new
            {
                name        = a.DisplayName,
                duration    = (long)a.TotalTime.TotalSeconds,
                percentage  = a.Percentage,
                productivity = a.ProductivityCategory,
                subcategory = a.Subcategory
            }).ToArray(),

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

            topAppsByExe = dashboard.TopAppsByExe.Select(a => new
            {
                name        = a.DisplayName,
                duration    = (long)a.TotalTime.TotalSeconds,
                percentage  = a.Percentage,
                productivity = a.ProductivityCategory,
                subcategory = a.Subcategory
            }).ToArray(),

            weeklyHistory = await BuildWeeklyHistoryAsync(targetDate, dashboard.TotalWorkTime.TotalHours, ct)
        };

        return SuccessResponse(requestId, summary);
    }

    /// <summary>
    /// Builds summary from backend cloud API (past days' authoritative data).
    /// Falls back to local SQLite if the backend is unreachable.
    /// </summary>
    private async Task<IpcResponse> BuildFromBackendApi(int requestId, DateTime targetDate, CancellationToken ct)
    {
        try
        {
            var report = await _reportsClient.GetDailySummaryAsync(targetDate, ct);
            if (report != null)
            {
                _logger.LogInformation("[GetTodaySummary] Got summary from backend for {Date}",
                    targetDate.ToString("yyyy-MM-dd"));

                // Filter out internal apps — use ProcessName (canonical) with DisplayName fallback
                // for responses from older backend versions that didn't include processName.
                var filteredApps = report.Apps
                    .Where(a => !InternalApps.Contains(
                        string.IsNullOrEmpty(a.ProcessName) ? a.DisplayName : a.ProcessName))
                    .ToList();

                var totalActiveSeconds = filteredApps.Sum(a => a.TotalSeconds);

                // Use cloud-resolved productivity directly (falls back to subcategory mapping)
                string resolveProductivity(DailyReportApp a) => a.Productivity ?? MapCategoryToProductivity(a.AppCategory);

                var productiveSeconds = filteredApps
                    .Where(a => resolveProductivity(a) == "productive")
                    .Sum(a => a.TotalSeconds);

                var summary = new
                {
                    totalDuration  = totalActiveSeconds,
                    productiveTime = productiveSeconds,
                    idleTime       = report.TotalIdleSeconds,
                    focusTime      = productiveSeconds,
                    focusScore     = totalActiveSeconds > 0
                        ? (int)Math.Round((double)productiveSeconds / totalActiveSeconds * 100)
                        : 0,
                    sessionsCount  = filteredApps.Sum(a => a.SessionCount),
                    topProjects    = Array.Empty<object>(),

                    topApplications = filteredApps.Select(a =>
                    {
                        var pct = totalActiveSeconds > 0
                            ? Math.Round((double)a.TotalSeconds / totalActiveSeconds * 100, 1)
                            : 0.0;
                        return new
                        {
                            name        = a.DisplayName,
                            duration    = a.TotalSeconds,
                            percentage  = pct,
                            productivity = resolveProductivity(a),
                            subcategory = a.AppCategory ?? "unknown"
                        };
                    }).OrderByDescending(a => a.duration).Take(10).ToArray(),

                    categories = filteredApps
                        .GroupBy(a =>
                        {
                            var cat = a.AppCategory;
                            if (string.IsNullOrEmpty(cat) || cat == "unknown")
                                return resolveProductivity(a);
                            if (cat == "browser_general")
                                return "other";
                            return cat;
                        })
                        .Select(g => new
                        {
                            name        = FormatCategoryName(g.Key),
                            duration    = g.Sum(a => a.TotalSeconds),
                            percentage  = totalActiveSeconds > 0
                                ? Math.Round(g.Sum(a => (double)a.TotalSeconds) / totalActiveSeconds * 100, 1)
                                : 0.0,
                            color       = GetCategoryColor(g.Key),
                            productivity = resolveProductivity(g.First())
                        })
                        .OrderByDescending(c => c.duration)
                        .ToArray(),

                    topAppsByExe = filteredApps.Select(a =>
                    {
                        var pct = totalActiveSeconds > 0
                            ? Math.Round((double)a.TotalSeconds / totalActiveSeconds * 100, 1)
                            : 0.0;
                        return new
                        {
                            name        = a.DisplayName,
                            duration    = a.TotalSeconds,
                            percentage  = pct,
                            productivity = resolveProductivity(a),
                            subcategory = a.AppCategory ?? "unknown"
                        };
                    }).OrderByDescending(a => a.duration).Take(10).ToArray(),

                    weeklyHistory = await BuildWeeklyHistoryAsync(targetDate, totalActiveSeconds / 3600.0, ct)
                };

                return SuccessResponse(requestId, summary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetTodaySummary] Backend fetch failed for {Date}, falling back to local",
                targetDate.ToString("yyyy-MM-dd"));
        }

        // Fallback: try local SQLite
        _logger.LogWarning("[GetTodaySummary] No data from backend for {Date}, falling back to local",
            targetDate.ToString("yyyy-MM-dd"));
        return await BuildFromLocalDashboard(requestId, targetDate, ct);
    }

    /// <summary>
    /// Maps an AppCategory to a productivity classification.
    /// Same logic as GetRecentActivitiesQueryHandler.MapCategoryToProductivity.
    /// </summary>
    private static string MapCategoryToProductivity(string? category) => category?.ToLowerInvariant() switch
    {
        "development" or "design" or "communication" or "productivity_tools" or "productivity"
            or "meetings" or "documentation" or "dev_ops" or "devops" or "finance"
            or "productive" => "productive",
        "social_media" or "entertainment" or "gaming" or "news"
            or "music_streaming" or "shopping"
            or "distraction" => "distraction",
        _ => "neutral"
    };

    /// <summary>
    /// Builds weekly history: today from local SQLite (real-time), past days from backend API (cloud).
    /// Falls back to local SQLite if the backend is unreachable.
    /// </summary>
    private async Task<object[]> BuildWeeklyHistoryAsync(DateTime centerDate, double todayHours, CancellationToken ct)
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
                hours = todayHours;
            }
            else
            {
                try
                {
                    var report = await _reportsClient.GetDailySummaryAsync(date, ct);
                    if (report != null)
                    {
                        var filteredSeconds = report.Apps
                            .Where(a => !InternalApps.Contains(
                                string.IsNullOrEmpty(a.ProcessName) ? a.DisplayName : a.ProcessName))
                            .Sum(a => a.TotalSeconds);
                        hours = filteredSeconds / 3600.0;
                    }
                    else
                    {
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

    private static string FormatCategoryName(string key)
    {
        if (string.IsNullOrEmpty(key)) return "Outros";

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

        return string.Join(' ', key.Split('_').Select(w =>
            w.Length > 0 ? char.ToUpper(w[0]) + w[1..] : w));
    }

    private static string GetCategoryColor(string key) =>
        key?.ToLowerInvariant() switch
        {
            "development"        => "#38bdf8",
            "design"             => "#a78bfa",
            "productivity_tools" => "#34d399",
            "productivity"       => "#34d399",
            "communication"      => "#fb923c",
            "meetings"           => "#22d3ee",
            "entertainment"      => "#f87171",
            "social_media"       => "#f472b6",
            "other"              => "#94a3b8",
            "productive"         => "#4ade80",
            "neutral"            => "#fbbf24",
            "distraction"        => "#ef4444",
            _                    => "#94a3b8"
        };
}
