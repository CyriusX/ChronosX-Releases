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
    private bool _updateInProgress;

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
        if (_updateInProgress)
        {
            _logger.LogDebug("Skipping update check - update already in progress");
            return;
        }

        _logger.LogInformation("Checking for updates...");

        try
        {
            var result = await _updateService.CheckForUpdatesAsync(cancellationToken);
            _lastCheck = DateTime.UtcNow;

            if (result?.HasUpdate == true)
            {
                _logger.LogInformation(
                    "Update available: {Version}. Starting forced update...",
                    result.LatestVersion);

                // Broadcast update available event
                await _eventBroadcaster.BroadcastUpdateAvailableAsync(result);

                // Start forced update with try/finally to guarantee _updateInProgress is reset
                _updateInProgress = true;
                try
                {
                    await _updateService.StartUpdateAsync(cancellationToken);
                }
                finally
                {
                    _updateInProgress = false;
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
            _updateInProgress = false;
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

    public override void Dispose()
    {
        _updateService.ProgressChanged -= OnUpdateProgressChanged;
        _updateService.UpdateAvailable -= OnUpdateAvailable;
        _updateService.UpdateCompleted -= OnUpdateCompleted;
        base.Dispose();
    }
}
