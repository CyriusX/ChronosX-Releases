using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Handles getting tasks list
/// </summary>
public sealed class GetTasksQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTasks";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from task repository
        return SuccessResponse(request.RequestId, Array.Empty<object>());
    }
}
