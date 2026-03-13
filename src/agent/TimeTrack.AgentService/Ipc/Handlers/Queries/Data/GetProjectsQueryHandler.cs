using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Handles getting projects list
/// </summary>
public sealed class GetProjectsQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetProjects";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from project repository
        return SuccessResponse(request.RequestId, Array.Empty<object>());
    }
}
