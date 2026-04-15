using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;

/// <summary>
/// Proxies PATCH /api/v1/tasks/{id}/move from the desktop UI through the agent.
/// Payload: { taskId: string, status?: string (Todo|InProgress|Done, default InProgress), rowVersion?: number }
///
/// This is a convenience IPC path; the desktop UI can also call the backend
/// REST endpoint directly via its own apiClient. Both are supported.
/// </summary>
public sealed class AssignTaskCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "AssignTask";

    private readonly IBackendTasksClient _backendTasks;
    private readonly ILogger<AssignTaskCommandHandler> _logger;

    public AssignTaskCommandHandler(IBackendTasksClient backendTasks, ILogger<AssignTaskCommandHandler> logger)
    {
        _backendTasks = backendTasks;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            if (!request.Payload.HasValue || !request.Payload.Value.TryGetProperty("taskId", out var taskIdProp))
                return ErrorResponse(request.RequestId, "taskId is required");

            if (!Guid.TryParse(taskIdProp.GetString(), out var taskId))
                return ErrorResponse(request.RequestId, "taskId must be a GUID");

            var status = "InProgress";
            if (request.Payload.Value.TryGetProperty("status", out var statusProp))
                status = statusProp.GetString() ?? status;

            uint? rowVersion = null;
            if (request.Payload.Value.TryGetProperty("rowVersion", out var rvProp) && rvProp.ValueKind == System.Text.Json.JsonValueKind.Number)
                rowVersion = rvProp.GetUInt32();

            var result = await _backendTasks.MoveTaskAsync(taskId, status, rowVersion, ct);
            if (result is null)
                return ErrorResponse(request.RequestId, "Backend refused the move (version conflict or network issue)");

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[AssignTask] failed");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
