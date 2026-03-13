using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Dashboard;

/// <summary>
/// Handles getting recent activities
/// </summary>
public sealed class GetRecentActivitiesQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetRecentActivities";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from activity session repository
        return SuccessResponse(request.RequestId, new
        {
            activities = Array.Empty<object>(),
            total = 0
        });
    }
}
