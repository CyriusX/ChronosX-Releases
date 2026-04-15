using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;

/// <summary>
/// Proxies POST /api/v1/me/tasks/open/close-on-idle-reject.
/// Called when the user clicks "No" on the task idle resume prompt —
/// the backend closes the open entry and moves the task back to Todo.
/// </summary>
public sealed class CloseOpenTaskCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "CloseOpenTask";

    private readonly IBackendTasksClient _backendTasks;
    private readonly ILogger<CloseOpenTaskCommandHandler> _logger;

    public CloseOpenTaskCommandHandler(IBackendTasksClient backendTasks, ILogger<CloseOpenTaskCommandHandler> logger)
    {
        _backendTasks = backendTasks;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var ok = await _backendTasks.CloseOpenTaskOnIdleRejectAsync(ct);
            return ok
                ? SuccessResponse(request.RequestId, new { closed = true })
                : ErrorResponse(request.RequestId, "Backend refused the close (network or auth issue)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CloseOpenTask] failed");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
