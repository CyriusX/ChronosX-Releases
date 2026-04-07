using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Update;

/// <summary>
/// Handles manual update check requests from the frontend
/// </summary>
public sealed class CheckForUpdatesCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "CheckForUpdates";

    private readonly IUpdateService _updateService;
    private readonly ILogger<CheckForUpdatesCommandHandler> _logger;

    public CheckForUpdatesCommandHandler(
        IUpdateService updateService,
        ILogger<CheckForUpdatesCommandHandler> logger)
    {
        _updateService = updateService;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Manual update check requested");

        try
        {
            if (_updateService.IsUpdating)
            {
                _logger.LogWarning("Update already in progress");
                return SuccessResponse(request.RequestId, new
                {
                    @checked = false,
                    reason = "update_in_progress",
                    currentProgress = _updateService.CurrentProgress
                });
            }

            var result = await _updateService.CheckForUpdatesAsync(ct);

            if (result == null)
            {
                _logger.LogWarning("Update check returned null");
                return SuccessResponse(request.RequestId, new
                {
                    @checked = true,
                    hasUpdate = false,
                    error = "Failed to check for updates"
                });
            }

            _logger.LogInformation(
                "Update check completed: HasUpdate={HasUpdate}, Version={Version}",
                result.HasUpdate,
                result.LatestVersion);

            return SuccessResponse(request.RequestId, new
            {
                @checked = true,
                hasUpdate = result.HasUpdate,
                currentVersion = result.CurrentVersion,
                latestVersion = result.LatestVersion,
                downloadUrl = result.DownloadUrl,
                fileSizeBytes = result.FileSizeBytes,
                releaseNotes = result.ReleaseNotes
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for updates");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
