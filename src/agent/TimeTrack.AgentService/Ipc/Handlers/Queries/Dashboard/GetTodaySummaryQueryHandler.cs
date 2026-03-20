using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.GetLocalDashboard;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Handles getting today's summary
/// </summary>
public sealed class GetTodaySummaryQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTodaySummary";

    private readonly GetLocalDashboardUseCase _getDashboard;
    private readonly ILogger<GetTodaySummaryQueryHandler> _logger;

    public GetTodaySummaryQueryHandler(
        GetLocalDashboardUseCase getDashboard,
        ILogger<GetTodaySummaryQueryHandler> logger)
    {
        _getDashboard = getDashboard;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var dashboard = await _getDashboard.ExecuteAsync(DateTime.Today, ct);

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

                // Group by subcategory (development, communication, …) for meaningful category cards.
                // Apps whose subcategory is still "unknown" fall back to their productivity level so
                // they are never silently dropped from the list.
                categories = dashboard.TopApplications
                    .GroupBy(a => a.Subcategory == "unknown" || string.IsNullOrEmpty(a.Subcategory)
                        ? a.ProductivityCategory
                        : a.Subcategory)
                    .Select(g => new
                    {
                        name        = g.Key,
                        duration    = (long)g.Sum(a => a.TotalTime.TotalSeconds),
                        percentage  = g.Sum(a => a.Percentage),
                        color       = GetCategoryColor(g.Key),
                        productivity = g.First().ProductivityCategory
                    })
                    .OrderByDescending(c => c.duration)
                    .ToArray(),

                weeklyHistory = WeeklyHistoryGenerator.Generate()
            };

            return SuccessResponse(request.RequestId, summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting today summary");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    private static string GetCategoryColor(string key) =>
        key?.ToLowerInvariant() switch
        {
            // Subcategory-based colours
            "development"        => "#4ad9ff",
            "design"             => "#8b7aff",
            "productivity_tools" => "#05df72",
            "communication"      => "#ff9c5b",
            "meetings"           => "#4ad9ff",
            "browser_general"    => "#fbbf24",
            "social_media"       => "#f87171",
            "entertainment"      => "#f87171",
            // Productivity-level fallbacks
            "productive"         => "#4ade80",
            "neutral"            => "#fbbf24",
            "distraction"        => "#f87171",
            _                    => "#94a3b8"
        };
}
