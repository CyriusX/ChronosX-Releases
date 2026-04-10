using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;

/// <summary>
/// Proxies POST /api/v1/me/tasks/open/resume to resume a paused task timer.
/// Called by the DesktopHost when the user accepts the "still working?" prompt.
/// </summary>
public sealed class ResumeOpenTaskCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ResumeOpenTask";

    private readonly IBackendTasksClient _backendTasks;
    private readonly ILogger<ResumeOpenTaskCommandHandler> _logger;

    public ResumeOpenTaskCommandHandler(IBackendTasksClient backendTasks, ILogger<ResumeOpenTaskCommandHandler> logger)
    {
        _backendTasks = backendTasks;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var ok = await _backendTasks.ResumeMyOpenTaskAsync(ct);
            return ok
                ? SuccessResponse(request.RequestId, new { resumed = true })
                : ErrorResponse(request.RequestId, "Backend refused the resume (network or auth issue)");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ResumeOpenTask] failed");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
