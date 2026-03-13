using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.GetSyncState;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.State;

/// <summary>
/// Handles getting sync state
/// </summary>
public sealed class GetSyncStateQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetSyncState";

    private readonly GetSyncStateUseCase _getSyncState;
    private readonly ILogger<GetSyncStateQueryHandler> _logger;

    public GetSyncStateQueryHandler(
        GetSyncStateUseCase getSyncState,
        ILogger<GetSyncStateQueryHandler> logger)
    {
        _getSyncState = getSyncState;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var state = await _getSyncState.ExecuteAsync(ct);
            return SuccessResponse(request.RequestId, state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync state");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
