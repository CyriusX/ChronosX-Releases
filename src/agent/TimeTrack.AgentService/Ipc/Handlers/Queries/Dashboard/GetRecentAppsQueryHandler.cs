using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Handles getting recent apps
/// </summary>
public sealed class GetRecentAppsQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetRecentApps";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get recent apps from sessions
        return SuccessResponse(request.RequestId, new
        {
            apps = Array.Empty<object>(),
            since = DateTime.UtcNow.AddDays(-1)
        });
    }
}
