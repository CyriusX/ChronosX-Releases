using System.Text.Json;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Data;

/// <summary>
/// Handles getting errors
/// </summary>
public sealed class GetErrorsQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetErrors";

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Get from error repository
        return SuccessResponse(request.RequestId, new
        {
            errors = Array.Empty<object>(),
            total = 0
        });
    }
}
