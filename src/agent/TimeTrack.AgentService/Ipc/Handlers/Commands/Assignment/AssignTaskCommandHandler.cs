using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;

/// <summary>
/// Handles task assignment
/// </summary>
public sealed class AssignTaskCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "AssignTask";

    private readonly ILogger<AssignTaskCommandHandler> _logger;

    public AssignTaskCommandHandler(ILogger<AssignTaskCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement task assignment
        return SuccessResponse(request.RequestId, new { assigned = true });
    }
}
