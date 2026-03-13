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

            var summary = new
            {
                totalDuration = (int)dashboard.TotalWorkTime.TotalMinutes,
                productiveTime = (int)dashboard.TotalWorkTime.TotalMinutes,
                idleTime = (int)dashboard.TotalIdleTime.TotalMinutes,
                focusTime = 0,
                sessionsCount = dashboard.SessionCount,
                topProjects = Array.Empty<object>(),
                topApplications = dashboard.TopApplications.Select(a => new
                {
                    name = a.DisplayName,
                    duration = (int)a.TotalTime.TotalMinutes,
                    percentage = a.Percentage
                }).ToArray(),
                categories = Array.Empty<object>(),
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
}
