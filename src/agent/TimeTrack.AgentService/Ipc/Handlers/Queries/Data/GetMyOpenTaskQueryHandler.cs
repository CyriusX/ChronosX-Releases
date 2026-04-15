using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Proxies GET /api/v1/me/tasks/open — returns the currently in-progress
/// task (if any) for the authenticated user. Used by the floating status
/// bar to render a live task section, and by the desktop UI to refresh
/// the current task indicator without hammering the backend directly.
/// </summary>
public sealed class GetMyOpenTaskQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetMyOpenTask";

    private readonly IBackendTasksClient _backendTasks;
    private readonly ILogger<GetMyOpenTaskQueryHandler> _logger;

    public GetMyOpenTaskQueryHandler(IBackendTasksClient backendTasks, ILogger<GetMyOpenTaskQueryHandler> logger)
    {
        _backendTasks = backendTasks;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _backendTasks.GetMyOpenTaskAsync(ct);
            // Null = no open task → return an empty object so the caller can check taskId == null
            return SuccessResponse(request.RequestId, (object?)result ?? new { });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetMyOpenTask] failed");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
