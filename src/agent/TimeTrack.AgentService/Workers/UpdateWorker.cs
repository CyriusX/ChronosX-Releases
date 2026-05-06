using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Contracts.Updates;
using TimeTrack.AgentService.Configuration;
using TimeTrack.AgentService.Events;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Background worker that periodically checks for application updates
/// </summary>
public sealed class UpdateWorker : BackgroundService
{
    private readonly ILogger<UpdateWorker> _logger;
    private readonly UpdateSettings _settings;
    private readonly IUpdateService _updateService;
    private readonly UpdateEventBroadcaster _eventBroadcaster;

    private DateTime _lastCheck = DateTime.MinValue;

    public UpdateWorker(
        ILogger<UpdateWorker> logger,
        UpdateSettings settings,
        IUpdateService updateService,
        UpdateEventBroadcaster eventBroadcaster)
    {
        _logger = logger;
        _settings = settings;
        _updateService = updateService;
        _eventBroadcaster = eventBroadcaster;

        // Subscribe to update service events
        _updateService.ProgressChanged += OnUpdateProgressChanged;
        _updateService.UpdateAvailable += OnUpdateAvailable;
        _updateService.UpdateCompleted += OnUpdateCompleted;
    }

    public DateTime LastCheck => _lastCheck;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Update worker disabled by configuration");
            return;
        }

        _logger.LogInformation(
            "UpdateWorker started. Check interval: {Interval}min, Channel: {Channel}",
            _settings.CheckIntervalMinutes,
            _settings.Channel);

        // Check if a previous update completed or failed while we were down
        await BroadcastPendingUpdateResultAsync(stoppingToken);

        // Check on startup if configured
        if (_settings.CheckOnStartup)
        {
            await CheckForUpdatesAsync(stoppingToken);
        }

        var checkInterval = TimeSpan.FromMinutes(_settings.CheckIntervalMinutes);

        using var periodicTimer = new PeriodicTimer(checkInterval);

        try
        {
            while (await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                await CheckForUpdatesAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UpdateWorker stopping");
        }
    }

    private async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking for updates...");

        try
        {
            var result = await _updateService.CheckForUpdatesAsync(cancellationToken);
            _lastCheck = DateTime.UtcNow;

            if (result?.HasUpdate == true)
            {
                _logger.LogInformation(
                    "Update available: {Version}. Notifying user...",
                    result.LatestVersion);

                await _eventBroadcaster.BroadcastUpdateAvailableAsync(result);

                // Only auto-start update when ForceUpdate is enabled
                if (_settings.ForceUpdate)
                {
                    _logger.LogInformation("ForceUpdate enabled — starting update automatically");
                    await _updateService.StartUpdateAsync(cancellationToken);
                }
            }
            else
            {
                _logger.LogDebug("No update available");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for updates");
        }
    }

    private void OnUpdateProgressChanged(object? sender, UpdateProgress progress)
    {
        // Broadcast progress to connected clients
        _ = _eventBroadcaster.BroadcastUpdateProgressAsync(progress);
    }

    private void OnUpdateAvailable(object? sender, UpdateCheckResponse update)
    {
        _logger.LogInformation("Update available event: {Version}", update.LatestVersion);
    }

    private void OnUpdateCompleted(object? sender, UpdateResult result)
    {
        if (result.Success)
        {
            _logger.LogInformation("Update completed successfully to version {Version}", result.Version);

            // Broadcast completion
            _ = _eventBroadcaster.BroadcastUpdateCompleteAsync(result.Version ?? "unknown");
        }
        else
        {
            _logger.LogError("Update failed: {Error}", result.ErrorMessage);

            // Broadcast failure
            _ = _eventBroadcaster.BroadcastUpdateFailedAsync(result.ErrorMessage ?? "Unknown error");
        }
    }

    /// <summary>
    /// Reads the update-status.json left by update.exe and broadcasts the
    /// result to connected IPC clients so the frontend can resolve its state
    /// after being killed and restarted by the installer.
    /// </summary>
    private async Task BroadcastPendingUpdateResultAsync(CancellationToken cancellationToken)
    {
        try
        {
            var statusPath = Path.Combine(Path.GetTempPath(), "ChronosX-Update", "update-status.json");
            if (!File.Exists(statusPath))
                return;

            var json = await File.ReadAllTextAsync(statusPath, cancellationToken);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;

            var stage = root.TryGetProperty("stage", out var s) ? s.GetString() : null;
            var version = root.TryGetProperty("version", out var v) ? v.GetString() : "unknown";
            var timestampStr = root.TryGetProperty("timestamp", out var t) ? t.GetString() : null;
            var error = root.TryGetProperty("error", out var e) ? e.GetString() : null;

            // Only act on recent status files (within last 30 minutes)
            if (DateTime.TryParse(timestampStr, out var timestamp))
            {
                if (DateTime.UtcNow - timestamp > TimeSpan.FromMinutes(30))
                {
                    _logger.LogDebug("Stale update status file from {Time}, ignoring", timestamp);
                    TryDeleteStatusFile(statusPath);
                    return;
                }
            }

            _logger.LogInformation("Found pending update status: Stage={Stage}, Version={Version}", stage, version);

            // Give the IPC server a moment to connect clients
            await Task.Delay(2000, cancellationToken);

            switch (stage?.ToLowerInvariant())
            {
                case "completed":
                    await _eventBroadcaster.BroadcastUpdateCompleteAsync(version, cancellationToken);
                    _logger.LogInformation("Broadcast pending update complete for v{Version}", version);
                    break;

                case "failed":
                    await _eventBroadcaster.BroadcastUpdateFailedAsync(error ?? "Update failed", false, cancellationToken);
                    _logger.LogInformation("Broadcast pending update failure: {Error}", error);
                    break;

                default:
                    // "installing", "startingservices" etc. — update may still be in progress,
                    // don't broadcast anything; the frontend stale timer will handle it.
                    break;
            }

            TryDeleteStatusFile(statusPath);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read broadcast pending update status");
        }
    }

    private static void TryDeleteStatusFile(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public override void Dispose()
    {
        _updateService.ProgressChanged -= OnUpdateProgressChanged;
        _updateService.UpdateAvailable -= OnUpdateAvailable;
        _updateService.UpdateCompleted -= OnUpdateCompleted;
        base.Dispose();
    }
}
