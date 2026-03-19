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
                totalDuration = (long)dashboard.TotalWorkTime.TotalSeconds,
                productiveTime = (long)dashboard.TotalWorkTime.TotalSeconds,
                idleTime = (long)dashboard.TotalIdleTime.TotalSeconds,
                focusTime = (long)TimeSpan.FromMilliseconds(dashboard.FocusTimeMs).TotalSeconds,
                focusScore = dashboard.FocusScore,
                sessionsCount = dashboard.SessionCount,
                topProjects = Array.Empty<object>(),
                topApplications = dashboard.TopApplications.Select(a => new
                {
                    name = a.DisplayName,
                    duration = (long)a.TotalTime.TotalSeconds,
                    percentage = a.Percentage,
                    category = a.ProductivityCategory
                }).ToArray(),
                categories = dashboard.TopApplications
                    .GroupBy(a => a.ProductivityCategory)
                    .Select(g => new
                    {
                        name = g.Key,
                        duration = (long)g.Sum(a => a.TotalTime.TotalSeconds),
                        percentage = g.Sum(a => a.Percentage),
                        color = GetCategoryColor(g.Key)
                    }).ToArray(),
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

    private static string GetCategoryColor(string category)
    {
        return category?.ToLowerInvariant() switch
        {
            "productive" or "focus" => "#4ade80",
            "distraction" or "distracted" => "#f87171",
            "neutral" => "#fbbf24",
            _ => "#94a3b8"
        };
    }
}
