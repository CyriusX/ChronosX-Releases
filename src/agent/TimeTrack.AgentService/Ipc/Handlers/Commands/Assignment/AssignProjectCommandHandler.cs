using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Assignment;

/// <summary>
/// Handles project assignment
/// </summary>
public sealed class AssignProjectCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "AssignProject";

    private readonly ILogger<AssignProjectCommandHandler> _logger;

    public AssignProjectCommandHandler(ILogger<AssignProjectCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Implement project assignment
        return SuccessResponse(request.RequestId, new { assigned = true });
    }
}
