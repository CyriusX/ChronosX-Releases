using Microsoft.Extensions.Logging;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;

/// <summary>
/// Deprecated in the kanban task workflow: activity sessions now inherit
/// project/task from the open TaskTimeEntry at ingest time. Kept for
/// backward compatibility so older desktop UI builds don't error out —
/// the handler accepts the call and reports success without doing work.
/// </summary>
public sealed class AssignProjectCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "AssignProject";

    private readonly ILogger<AssignProjectCommandHandler> _logger;

    public AssignProjectCommandHandler(ILogger<AssignProjectCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogDebug("AssignProject IPC call is a no-op — projects are now inferred from kanban tasks.");
        return Task.FromResult(SuccessResponse(request.RequestId, new { assigned = true, deprecated = true }));
    }
}
