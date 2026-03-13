using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Handles getting current session
/// </summary>
public sealed class GetCurrentSessionQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetCurrentSession";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get current session from repository
        return SuccessResponse(request.RequestId, new
        {
            id = Guid.NewGuid().ToString(),
            projectName = "Current Project",
            startedAt = DateTime.UtcNow.AddHours(-1),
            duration = 3600,
            isIdle = false,
            isActive = true
        });
    }
}
