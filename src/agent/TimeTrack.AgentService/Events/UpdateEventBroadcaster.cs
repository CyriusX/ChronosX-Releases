using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Updates;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Events;

/// <summary>
/// Broadcasts update-related events to connected IPC clients
/// </summary>
public sealed class UpdateEventBroadcaster
{
    private readonly IIpcServer _ipcServer;
    private readonly ILogger<UpdateEventBroadcaster> _logger;

    public UpdateEventBroadcaster(
        IIpcServer ipcServer,
        ILogger<UpdateEventBroadcaster> logger)
    {
        _ipcServer = ipcServer;
        _logger = logger;
    }

    /// <summary>
    /// Broadcasts that an update is available
    /// </summary>
    public async Task BroadcastUpdateAvailableAsync(
        UpdateCheckResponse updateInfo,
        CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
            return;

        try
        {
            _logger.LogInformation(
                "Broadcasting UpdateAvailable: Version={Version}",
                updateInfo.LatestVersion);

            var payload = new
            {
                hasUpdate = updateInfo.HasUpdate,
                currentVersion = updateInfo.CurrentVersion,
                latestVersion = updateInfo.LatestVersion,
                downloadUrl = updateInfo.DownloadUrl,
                checksumSha256 = updateInfo.ChecksumSha256,
                fileSizeBytes = updateInfo.FileSizeBytes,
                releaseNotes = updateInfo.ReleaseNotes,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "updateAvailable",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting UpdateAvailable event");
        }
    }

    /// <summary>
    /// Broadcasts update progress
    /// </summary>
    public async Task BroadcastUpdateProgressAsync(
        UpdateProgress progress,
        CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
            return;

        try
        {
            _logger.LogDebug(
                "Broadcasting UpdateProgress: Stage={Stage}, Percentage={Percentage}%",
                progress.Stage, progress.Percentage);

            var payload = new
            {
                stage = progress.Stage.ToString().ToLowerInvariant(),
                percentage = progress.Percentage,
                message = progress.Message,
                bytesDownloaded = progress.BytesDownloaded,
                bytesTotal = progress.BytesTotal,
                bytesPerSecond = progress.BytesPerSecond,
                targetVersion = progress.TargetVersion,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "updateProgress",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting UpdateProgress event");
        }
    }

    /// <summary>
    /// Broadcasts that an update has completed successfully
    /// </summary>
    public async Task BroadcastUpdateCompleteAsync(
        string version,
        CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
            return;

        try
        {
            _logger.LogInformation(
                "Broadcasting UpdateComplete: Version={Version}",
                version);

            var payload = new
            {
                success = true,
                version,
                restartRequired = true,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "updateComplete",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting UpdateComplete event");
        }
    }

    /// <summary>
    /// Broadcasts that an update has failed
    /// </summary>
    public async Task BroadcastUpdateFailedAsync(
        string errorMessage,
        bool canRollback = false,
        CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
            return;

        try
        {
            _logger.LogInformation(
                "Broadcasting UpdateFailed: Error={Error}",
                errorMessage);

            var payload = new
            {
                success = false,
                error = errorMessage,
                canRollback,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "updateFailed",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting UpdateFailed event");
        }
    }
}
