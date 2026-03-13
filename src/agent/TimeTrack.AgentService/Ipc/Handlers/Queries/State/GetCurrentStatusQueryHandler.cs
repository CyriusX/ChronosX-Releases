using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Handles getting current status
/// </summary>
public sealed class GetCurrentStatusQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetCurrentStatus";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        return SuccessResponse(request.RequestId, new
        {
            state = "running",
            uptime = (int)(DateTime.UtcNow - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalSeconds,
            version = "1.0.0"
        });
    }
}
