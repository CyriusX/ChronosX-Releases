using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Proxies GET /api/v1/me/tasks from the desktop UI through the agent.
/// Payload: { includeDone?: bool } — defaults to false.
/// </summary>
public sealed class GetTasksQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    private readonly IBackendTasksClient _backendTasks;
    private readonly ILogger<GetTasksQueryHandler> _logger;

    public string QueryName => "GetTasks";

    public GetTasksQueryHandler(IBackendTasksClient backendTasks, ILogger<GetTasksQueryHandler> logger)
    {
        _backendTasks = backendTasks;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var includeDone = false;
            if (request.Payload.HasValue && request.Payload.Value.TryGetProperty("includeDone", out var prop))
                includeDone = prop.GetBoolean();

            var result = await _backendTasks.ListMyTasksAsync(includeDone, ct);
            if (result is null)
                return SuccessResponse(request.RequestId, new { tasks = Array.Empty<object>(), todoCount = 0, inProgressCount = 0, doneCount = 0 });

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetTasks] failed");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
