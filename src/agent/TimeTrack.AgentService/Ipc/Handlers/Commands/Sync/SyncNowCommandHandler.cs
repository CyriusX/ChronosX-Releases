using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Sync;

/// <summary>
/// Handles triggering sync
/// </summary>
public sealed class SyncNowCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "SyncNow";

    private readonly ILogger<SyncNowCommandHandler> _logger;

    public SyncNowCommandHandler(ILogger<SyncNowCommandHandler> logger)
    {
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        // TODO: Trigger sync via SyncWorker
        return SuccessResponse(request.RequestId, new { syncTriggered = true });
    }
}
