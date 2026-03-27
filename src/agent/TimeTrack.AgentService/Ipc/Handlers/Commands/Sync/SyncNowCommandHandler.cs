using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Sync;

/// <summary>
/// Handles triggering immediate category cache sync from backend.
/// Called by the frontend after saving a category override so the
/// dashboard and activities reflect the change instantly.
/// Uses IServiceProvider to resolve the transient sync service at call time
/// (avoids captive dependency — this handler is a singleton).
/// </summary>
public sealed class SyncNowCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "SyncNow";

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncNowCommandHandler> _logger;

    public SyncNowCommandHandler(
        IServiceProvider serviceProvider,
        ILogger<SyncNowCommandHandler> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("SyncNow command received — forcing category cache sync");

        try
        {
            var syncService = _serviceProvider.GetService<IAppCategorySyncService>();
            if (syncService == null)
            {
                _logger.LogWarning("IAppCategorySyncService not registered, skipping sync");
                return SuccessResponse(request.RequestId, new { syncTriggered = false, success = false });
            }

            var success = await syncService.ForceSyncAsync(ct);
            _logger.LogInformation("Category sync {Result}", success ? "succeeded" : "failed");
            return SuccessResponse(request.RequestId, new { syncTriggered = true, success });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Category sync failed");
            return SuccessResponse(request.RequestId, new { syncTriggered = true, success = false });
        }
    }
}
