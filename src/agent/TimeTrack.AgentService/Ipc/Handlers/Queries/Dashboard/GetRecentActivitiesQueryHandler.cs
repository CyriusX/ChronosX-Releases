using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Returns activity sessions grouped by executable (app).
/// Always fetches from the cloud backend first (authoritative, safe source).
/// Falls back to local SQLite only when the backend is unavailable.
/// This ensures the Activity page always shows complete data even after a DB reset or reinstall.
/// Consecutive sessions for the same exe are merged into a single block.
/// Each block includes the list of window titles (tabs) used during that period.
/// </summary>
public sealed class GetRecentActivitiesQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetRecentActivities";

    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IAppCategoryCacheRepository _categoryCacheRepository;
    private readonly IBackendReportsClient _reportsClient;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<GetRecentActivitiesQueryHandler> _logger;

    private static readonly HashSet<string> InternalApps = new(StringComparer.OrdinalIgnoreCase)
    {
        "TimeTrack.DesktopHost",
        "ChronosX TimeTrack",
        "TimeTrack",
        "TimeTrack.MacOSAgentService",
        "Microsoft Edge WebView2",
        "Microsoft® Windows® Operating System",
        "Sistema operacional Microsoft® Windows®",
        // "Tracking Stopped" is intentionally NOT filtered here — it must appear in the
        // activity timeline so the UI can display paused periods as red blocks.
        // The 1-second placeholder is stretched to "now" by ActivitySection when rawDuration < 10s.
    };

    public GetRecentActivitiesQueryHandler(
        IActivitySessionRepository sessionRepository,
        IAppCategoryCacheRepository categoryCacheRepository,
        IBackendReportsClient reportsClient,
        ICurrentUserContext userContext,
        ILogger<GetRecentActivitiesQueryHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _categoryCacheRepository = categoryCacheRepository;
        _reportsClient = reportsClient;
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

            // Always fetch from cloud first — it is the authoritative, safe copy.
            // If the backend is unreachable, BuildFromBackendApi falls back to local SQLite.
            return await BuildFromBackendApi(request.RequestId, targetDate, userId.Value, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting recent activities");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    /// <summary>
    /// Builds activity blocks from local SQLite sessions (today's data).
    /// </summary>
    private async Task<IpcResponse> BuildFromLocalSqlite(int requestId, Guid userId, DateTime targetDate, CancellationToken ct)
    {
        var sessions = await _sessionRepository.GetByDateAsync(userId, targetDate, ct);

        // Build override lookup from local cache — never let failure break activities
        CategoryLookup categoryLookup;
        try
        {
            var cacheEntries = await _categoryCacheRepository.GetAllAsync();
            categoryLookup = BuildCategoryLookup(cacheEntries);
        }
        catch
        {
            categoryLookup = new CategoryLookup();
        }

        var filtered = sessions
            .Where(s => !InternalApps.Contains(s.App.DisplayName))
            .OrderBy(s => s.Period.StartUtc)
            .ToList();

        var appColors = AssignAppColors(filtered);
        var blocks = MergeConsecutiveByExe(filtered, appColors, categoryLookup);

        return SuccessResponse(requestId, new { activities = blocks });
    }

    /// <summary>
    /// Builds activity blocks from backend cloud API sessions (past days).
    /// Falls back to local SQLite if the backend is unreachable.
    /// </summary>
    private async Task<IpcResponse> BuildFromBackendApi(int requestId, DateTime targetDate, Guid userId, CancellationToken ct)
    {
        try
        {
            var result = await _reportsClient.GetDailyActivitiesAsync(targetDate, ct);
            if (result != null && result.Sessions.Count > 0)
            {
                _logger.LogInformation("[GetRecentActivities] Got {Count} sessions from backend for {Date}",
                    result.Sessions.Count, targetDate.ToString("yyyy-MM-dd"));

                var filtered = result.Sessions
                    .Where(s => !InternalApps.Contains(s.ProcessName))
                    .OrderBy(s => s.StartedAt)
                    .ToList();

                var appColors = AssignCloudAppColors(filtered);
                var blocks = MergeConsecutiveCloudSessions(filtered, appColors);

                return SuccessResponse(requestId, new { activities = blocks });
            }

            _logger.LogWarning("[GetRecentActivities] No data from backend for {Date}, falling back to local",
                targetDate.ToString("yyyy-MM-dd"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[GetRecentActivities] Backend fetch failed for {Date}, falling back to local",
                targetDate.ToString("yyyy-MM-dd"));
        }

        // Fallback: try local SQLite (may have data if cleanup hasn't run)
        return await BuildFromLocalSqlite(requestId, userId, targetDate, ct);
    }

    // ============================================================================
    // CLOUD SESSION MERGING (same logic as local, adapted for cloud DTOs)
    // ============================================================================

    private static Dictionary<string, string> AssignCloudAppColors(List<DailyActivitySession> sessions)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var idx = 0;
        foreach (var s in sessions)
        {
            if (!map.ContainsKey(s.ProcessName))
            {
                map[s.ProcessName] = AppPalette[idx % AppPalette.Length];
                idx++;
            }
        }
        return map;
    }

    private static object[] MergeConsecutiveCloudSessions(List<DailyActivitySession> sessions, Dictionary<string, string> appColors)
    {
        if (sessions.Count == 0) return Array.Empty<object>();

        var result = new List<object>();
        var currentApp = sessions[0].ProcessName;
        var currentStart = sessions[0].StartedAt;
        var currentEnd = sessions[0].EndedAt;
        var currentColor = appColors.GetValueOrDefault(currentApp, "#94a3b8");
        var currentCategory = sessions[0].AppCategory ?? "unknown";
        var tabs = new List<CloudTabInfo>();
        var currentAppName = ExtractCloudAppName(sessions[0]);

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
                productivity = MapCategoryToProductivity(currentCategory),
                subcategory = currentCategory,
                color = currentColor,
                tabs = uniqueTabs
            });
        }

        AddCloudTab(tabs, sessions[0]);

        for (int i = 1; i < sessions.Count; i++)
        {
            var s = sessions[i];
            var gap = (s.StartedAt - currentEnd).TotalSeconds;

            if (string.Equals(s.ProcessName, currentApp, StringComparison.OrdinalIgnoreCase) && gap < 120)
            {
                currentEnd = s.EndedAt > currentEnd ? s.EndedAt : currentEnd;
                AddCloudTab(tabs, s);
            }
            else
            {
                FlushBlock();

                currentApp = s.ProcessName;
                currentStart = s.StartedAt;
                currentEnd = s.EndedAt;
                currentColor = appColors.GetValueOrDefault(s.ProcessName, "#94a3b8");
                currentCategory = s.AppCategory ?? "unknown";
                currentAppName = ExtractCloudAppName(s);
                tabs = new List<CloudTabInfo>();
                AddCloudTab(tabs, s);
            }
        }

        FlushBlock();
        return result.ToArray();
    }

    private static void AddCloudTab(List<CloudTabInfo> tabs, DailyActivitySession session)
    {
        var title = !string.IsNullOrWhiteSpace(session.WindowTitle) ? session.WindowTitle : session.ProcessName;
        tabs.Add(new CloudTabInfo
        {
            Title = title,
            DurationSec = session.DurationSeconds,
            Subcategory = session.AppCategory ?? "unknown",
            Color = GetColor(session.AppCategory ?? "unknown")
        });
    }

    private static string ExtractCloudAppName(DailyActivitySession session)
    {
        // For browsers, show "Safari - YouTube" format
        if (BrowserDisplayNames.Contains(session.ProcessName)
            && !string.IsNullOrWhiteSpace(session.WindowTitle))
        {
            var pageTitle = ExtractPageTitle(session.WindowTitle, session.ProcessName);
            if (!string.IsNullOrEmpty(pageTitle) && pageTitle != session.WindowTitle)
            {
                if (pageTitle.Length > 40)
                    pageTitle = pageTitle[..37] + "...";
                return $"{session.ProcessName} - {pageTitle}";
            }
        }

        return session.ProcessName;
    }

    private static string MapCategoryToProductivity(string category) => category?.ToLowerInvariant() switch
    {
        "development" or "design" or "communication" or "productivity_tools" or "productivity"
            or "meetings" or "documentation" or "dev_ops" or "devops" or "finance"
            or "productive" => "productive",
        "social_media" or "entertainment" or "gaming" or "news"
            or "music_streaming" or "shopping"
            or "distraction" => "distraction",
        _ => "neutral"
    };

    private sealed class CloudTabInfo
    {
        public string Title { get; init; } = "";
        public int DurationSec { get; init; }
        public string Subcategory { get; init; } = "";
        public string Color { get; init; } = "";
    }

    // ============================================================================
    // LOCAL SESSION MERGING (original logic, unchanged)
    // ============================================================================

    private static readonly string[] AppPalette =
    {
        "#38bdf8", "#f472b6", "#34d399", "#fb923c", "#a78bfa",
        "#fbbf24", "#22d3ee", "#f87171", "#4ade80", "#e879f9",
        "#60a5fa", "#facc15", "#2dd4bf", "#f97316", "#c084fc",
        "#38bdf8",
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

    private static object[] MergeConsecutiveByExe(List<ActivitySession> sessions, Dictionary<string, string> appColors, CategoryLookup categoryLookup)
    {
        if (sessions.Count == 0) return Array.Empty<object>();

        var result = new List<object>();
        var currentExe = sessions[0].App.ExePathHash;
        var currentStart = sessions[0].Period.StartUtc;
        var currentEnd = sessions[0].Period.EndUtc;
        var currentColor = appColors.GetValueOrDefault(currentExe, "#94a3b8");
        var (initProd, initSub) = ResolveCategory(sessions[0], categoryLookup);
        var currentProductivity = initProd;
        var currentSubcategory = initSub;
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

        AddTab(tabs, sessions[0], categoryLookup);

        for (int i = 1; i < sessions.Count; i++)
        {
            var s = sessions[i];
            var gap = (s.Period.StartUtc - currentEnd).TotalSeconds;

            if (s.App.ExePathHash == currentExe && gap < 120)
            {
                currentEnd = s.Period.EndUtc > currentEnd ? s.Period.EndUtc : currentEnd;
                AddTab(tabs, s, categoryLookup);
            }
            else
            {
                FlushBlock();

                currentExe = s.App.ExePathHash;
                currentStart = s.Period.StartUtc;
                currentEnd = s.Period.EndUtc;
                currentColor = appColors.GetValueOrDefault(s.App.ExePathHash, "#94a3b8");
                var (prod, sub) = ResolveCategory(s, categoryLookup);
                currentProductivity = prod;
                currentSubcategory = sub;
                currentAppName = ExtractAppName(s);
                tabs = new List<TabInfo>();
                AddTab(tabs, s, categoryLookup);
            }
        }

        FlushBlock();
        return result.ToArray();
    }

    private static void AddTab(List<TabInfo> tabs, ActivitySession session, CategoryLookup categoryLookup)
    {
        // Use the window title for the tab name so browser tabs show the page title
        // (e.g. "YouTube", "Gmail") instead of just the app name ("Safari").
        // For non-browser apps, fall back to the display name.
        var title = !string.IsNullOrWhiteSpace(session.WindowTitle)
            ? ExtractPageTitle(session.WindowTitle, session.App.DisplayName)
            : session.App.DisplayName;
        var (prod, sub) = ResolveCategory(session, categoryLookup);

        tabs.Add(new TabInfo
        {
            Title = title,
            DurationSec = (long)session.Duration.TotalSeconds,
            Subcategory = sub,
            Color = GetColor(sub ?? prod)
        });
    }

    /// <summary>
    /// Extracts the page/document title from a window title by removing the app name suffix.
    /// "YouTube - Safari" → "YouTube"
    /// "index.ts - ProjectName - Visual Studio Code" → "index.ts - ProjectName"
    /// </summary>
    private static string ExtractPageTitle(string windowTitle, string appDisplayName)
    {
        var separators = new[] { " - ", " — ", " – " };
        foreach (var sep in separators)
        {
            var lastIdx = windowTitle.LastIndexOf(sep, StringComparison.Ordinal);
            if (lastIdx > 0)
            {
                var suffix = windowTitle[(lastIdx + sep.Length)..].Trim();
                // If the suffix matches the app name, strip it to get just the page title
                if (string.Equals(suffix, appDisplayName, StringComparison.OrdinalIgnoreCase))
                    return windowTitle[..lastIdx].Trim();
            }
        }
        return windowTitle;
    }

    private static readonly HashSet<string> BrowserDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google Chrome", "Chrome", "Safari", "Firefox", "Brave Browser", "Brave",
        "Microsoft Edge", "Opera", "Chromium", "Arc", "Vivaldi", "Orion",
    };

    private static string ExtractAppName(ActivitySession session)
    {
        // For browsers, show "Safari - YouTube" format so the user can see
        // which site they were on. For other apps, just show the app name.
        if (BrowserDisplayNames.Contains(session.App.DisplayName)
            && !string.IsNullOrWhiteSpace(session.WindowTitle))
        {
            var pageTitle = ExtractPageTitle(session.WindowTitle, session.App.DisplayName);
            if (!string.IsNullOrEmpty(pageTitle) && pageTitle != session.WindowTitle)
            {
                // Truncate long page titles
                if (pageTitle.Length > 40)
                    pageTitle = pageTitle[..37] + "...";
                return $"{session.App.DisplayName} - {pageTitle}";
            }
        }

        return session.App.DisplayName;
    }

    private static string GetColor(string key) => key?.ToLowerInvariant() switch
    {
        "development"        => "#38bdf8",
        "design"             => "#a78bfa",
        "productivity_tools" => "#34d399",
        "communication"      => "#fb923c",
        "meetings"           => "#22d3ee",
        "entertainment"      => "#f87171",
        "social_media"       => "#f472b6",
        "productive"         => "#4ade80",
        "neutral"            => "#fbbf24",
        "distraction"        => "#ef4444",
        _                    => "#94a3b8"
    };

    private class TabInfo
    {
        public string Title { get; init; } = "";
        public long DurationSec { get; init; }
        public string Subcategory { get; init; } = "";
        public string Color { get; init; } = "";
    }

    // ============================================================================
    // CATEGORY OVERRIDE RESOLUTION
    // ============================================================================

    private sealed class CategoryLookup
    {
        public Dictionary<string, (string Productivity, string Subcategory)> ByDisplayName { get; init; } = new();
        public Dictionary<string, (string Productivity, string Subcategory)> ByDomain { get; init; } = new();
        public bool IsEmpty => ByDisplayName.Count == 0 && ByDomain.Count == 0;
    }

    private static CategoryLookup BuildCategoryLookup(IReadOnlyList<AppCategoryCache> cacheEntries)
    {
        var byDisplayName = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);
        var byDomain = new Dictionary<string, (string, string)>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in cacheEntries)
        {
            var productivity = entry.Productivity switch
            {
                AppProductivityCategory.Productive => "productive",
                AppProductivityCategory.Distraction => "distraction",
                _ => "neutral"
            };
            var subcategory = entry.Subcategory ?? "unknown";

            if (!string.IsNullOrEmpty(entry.DisplayName))
                byDisplayName[entry.DisplayName] = (productivity, subcategory);

            if (entry.IdentifierType == AppIdentifierType.Domain && !string.IsNullOrEmpty(entry.Identifier))
                byDomain[entry.Identifier] = (productivity, subcategory);
        }

        return new CategoryLookup { ByDisplayName = byDisplayName, ByDomain = byDomain };
    }

    private static (string Productivity, string Subcategory) ResolveCategory(
        ActivitySession session, CategoryLookup lookup)
    {
        if (!lookup.IsEmpty)
        {
            if (!string.IsNullOrEmpty(session.Domain) &&
                lookup.ByDomain.TryGetValue(session.Domain, out var domainMatch))
                return domainMatch;

            if (lookup.ByDisplayName.TryGetValue(session.App.DisplayName, out var nameMatch))
                return nameMatch;
        }

        return (session.App.Category.Productivity, session.App.Category.Subcategory ?? "unknown");
    }
}
