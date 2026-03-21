using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Returns today's activity sessions grouped by executable (app).
/// Consecutive sessions for the same exe are merged into a single block.
/// Each block includes the list of window titles (tabs) used during that period.
/// </summary>
public sealed class GetRecentActivitiesQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetRecentActivities";

    private readonly IActivitySessionRepository _sessionRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<GetRecentActivitiesQueryHandler> _logger;

    private static readonly HashSet<string> InternalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimeTrack.DesktopHost",
        "Microsoft Edge WebView2",
        "Microsoft® Windows® Operating System"
    };

    public GetRecentActivitiesQueryHandler(
        IActivitySessionRepository sessionRepository,
        ICurrentUserContext userContext,
        ILogger<GetRecentActivitiesQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        var userId = _userContext.UserId;
        if (!userId.HasValue)
            return SuccessResponse(request.RequestId, new { activities = Array.Empty<object>() });

        try
        {
            var targetDate = ExtractDateOrToday(request);
            var sessions = await _sessionRepository.GetByDateAsync(userId.Value, targetDate, ct);

            var filtered = sessions
                .Where(s => !InternalApps.Contains(s.App.DisplayName))
                .OrderBy(s => s.Period.StartUtc)
                .ToList();

            // Assign a unique color per app (by ExePathHash)
            var appColors = AssignAppColors(filtered);

            // Group consecutive sessions by ExePathHash into app-level blocks.
            var blocks = MergeConsecutiveByExe(filtered, appColors);

            return SuccessResponse(request.RequestId, new { activities = blocks });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activities");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    // Distinct color palette for per-app coloring
    private static readonly string[] AppPalette =
    {
        "#38bdf8", // sky
        "#f472b6", // pink
        "#34d399", // emerald
        "#fb923c", // orange
        "#a78bfa", // violet
        "#fbbf24", // amber
        "#22d3ee", // cyan
        "#f87171", // red
        "#4ade80", // green
        "#e879f9", // fuchsia
        "#60a5fa", // blue
        "#facc15", // yellow
        "#2dd4bf", // teal
        "#f97316", // orange-500
        "#c084fc", // purple
        "#38bdf8", // sky (wraps)
    };

    private static Dictionary<string, string> AssignAppColors(List<ActivitySession> sessions)
    {
        var map = new Dictionary<string, string>();
        var idx = 0;
        foreach (var s in sessions)
        {
            if (!map.ContainsKey(s.App.ExePathHash))
            {
                map[s.App.ExePathHash] = AppPalette[idx % AppPalette.Length];
                idx++;
            }
        }
        return map;
    }

    private static object[] MergeConsecutiveByExe(List<ActivitySession> sessions, Dictionary<string, string> appColors)
    {
        if (sessions.Count == 0) return Array.Empty<object>();

        var result = new List<object>();
        var currentExe = sessions[0].App.ExePathHash;
        var currentStart = sessions[0].Period.StartUtc;
        var currentEnd = sessions[0].Period.EndUtc;
        var currentColor = appColors.GetValueOrDefault(currentExe, "#94a3b8");
        var currentProductivity = sessions[0].App.Category.Productivity;
        var currentSubcategory = sessions[0].App.Category.Subcategory ?? "unknown";
        var tabs = new List<TabInfo>();

        var currentAppName = ExtractAppName(sessions[0]);

        void FlushBlock()
        {
            var uniqueTabs = tabs
                .GroupBy(t => t.Title)
                .Select(g => new
                {
                    title = g.Key,
                    duration = g.Sum(t => t.DurationSec),
                    subcategory = g.First().Subcategory,
                    color = g.First().Color
                })
                .OrderByDescending(t => t.duration)
                .Take(8)
                .ToArray();

            result.Add(new
            {
                id = Guid.NewGuid().ToString(),
                name = currentAppName,
                startUtc = currentStart.ToString("o"),
                endUtc = currentEnd.ToString("o"),
                duration = (long)(currentEnd - currentStart).TotalSeconds,
                productivity = currentProductivity,
                subcategory = currentSubcategory,
                color = currentColor,
                tabs = uniqueTabs
            });
        }

        // Add first session's tab
        AddTab(tabs, sessions[0]);

        for (int i = 1; i < sessions.Count; i++)
        {
            var s = sessions[i];
            var gap = (s.Period.StartUtc - currentEnd).TotalSeconds;

            // Same exe and gap < 2 minutes → merge
            if (s.App.ExePathHash == currentExe && gap < 120)
            {
                currentEnd = s.Period.EndUtc > currentEnd ? s.Period.EndUtc : currentEnd;
                AddTab(tabs, s);
            }
            else
            {
                // Different app or big gap → flush and start new block
                FlushBlock();

                currentExe = s.App.ExePathHash;
                currentStart = s.Period.StartUtc;
                currentEnd = s.Period.EndUtc;
                currentColor = appColors.GetValueOrDefault(s.App.ExePathHash, "#94a3b8");
                currentProductivity = s.App.Category.Productivity;
                currentSubcategory = s.App.Category.Subcategory ?? "unknown";
                currentAppName = ExtractAppName(s);
                tabs = new List<TabInfo>();
                AddTab(tabs, s);
            }
        }

        FlushBlock();
        return result.ToArray();
    }

    private static void AddTab(List<TabInfo> tabs, ActivitySession session)
    {
        // For browser tabs, DisplayName is the tab title.
        // For regular apps, DisplayName is the app name — use WindowTitle if available for more detail.
        var title = session.App.DisplayName;

        tabs.Add(new TabInfo
        {
            Title = title,
            DurationSec = (long)session.Duration.TotalSeconds,
            Subcategory = session.App.Category.Subcategory ?? "unknown",
            Color = GetColor(session.App.Category.Subcategory ?? session.App.Category.Productivity)
        });
    }

    /// <summary>
    /// Gets the exe-level app name. For browsers, extracts from WindowTitle suffix.
    /// </summary>
    private static string ExtractAppName(ActivitySession session)
    {
        var windowTitle = session.WindowTitle;
        if (!string.IsNullOrWhiteSpace(windowTitle))
        {
            var separators = new[] { " - ", " — ", " – " };
            foreach (var sep in separators)
            {
                var lastIdx = windowTitle.LastIndexOf(sep, StringComparison.Ordinal);
                if (lastIdx > 0)
                {
                    var suffix = windowTitle[(lastIdx + sep.Length)..].Trim();
                    if (suffix.Length > 2 && suffix != session.App.DisplayName)
                        return suffix;
                }
            }
        }
        return session.App.DisplayName;
    }

    private static string GetColor(string key) => key?.ToLowerInvariant() switch
    {
        "development"        => "#38bdf8",   // sky blue
        "design"             => "#a78bfa",   // violet
        "productivity_tools" => "#34d399",   // emerald
        "communication"      => "#fb923c",   // orange
        "meetings"           => "#22d3ee",   // cyan
        "entertainment"      => "#f87171",   // red
        "social_media"       => "#f472b6",   // pink
        "productive"         => "#4ade80",   // green
        "neutral"            => "#fbbf24",   // amber
        "distraction"        => "#ef4444",   // red-600
        _                    => "#94a3b8"
    };

    private class TabInfo
    {
        public string Title { get; init; } = "";
        public long DurationSec { get; init; }
        public string Subcategory { get; init; } = "";
        public string Color { get; init; } = "";
    }
}
