using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Background service that listens to FocusModeEngine.StateChanged events
/// and broadcasts them to connected IPC clients (DesktopHost)
///
/// SOLID:
/// - SRP: Only bridges FocusModeEngine events to IPC
/// - OCP: Extensible for other event types
/// - DIP: Depends on IFocusModeEngine and IIpcServer abstractions
/// </summary>
public sealed class FocusModeEventBroadcaster : BackgroundService
{
    private readonly IFocusModeEngine _focusModeEngine;
    private readonly IIpcServer _ipcServer;
    private readonly ILogger<FocusModeEventBroadcaster> _logger;

    public FocusModeEventBroadcaster(
        IFocusModeEngine focusModeEngine,
        IIpcServer ipcServer,
        ILogger<FocusModeEventBroadcaster> logger)
    {
        _focusModeEngine = focusModeEngine;
        _ipcServer = ipcServer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FocusModeEventBroadcaster started");

        // Subscribe to state changes
        _focusModeEngine.StateChanged += OnFocusModeStateChanged;

        try
        {
            // Keep the service running until cancellation is requested
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        finally
        {
            _focusModeEngine.StateChanged -= OnFocusModeStateChanged;
            _logger.LogInformation("FocusModeEventBroadcaster stopped");
        }
    }

    private async void OnFocusModeStateChanged(object? sender, FocusModeStateChangedEventArgs e)
    {
        try
        {
            _logger.LogInformation(
                "Broadcasting FocusModeStateChanged: {PreviousState} -> {CurrentState}, Mode={Mode}",
                e.PreviousState,
                e.CurrentState,
                e.Snapshot.Mode);

            var payload = new
            {
                state = e.CurrentState.ToString(),
                previousState = e.PreviousState.ToString(),
                mode = e.Snapshot.Mode.ToString(),
                remainingMs = e.Snapshot.RemainingMs,
                cycleNumber = e.Snapshot.CycleNumber,
                totalCyclesToday = e.Snapshot.TotalCyclesToday,
                nextBreakType = e.Snapshot.NextBreakType.ToString(),
                cycleStartedAt = e.Snapshot.CycleStartedAt?.ToString("O"),
                plannedDurationMs = e.Snapshot.PlannedDurationMs,
                allowUserOverride = e.Snapshot.AllowUserOverride,
                reason = e.Reason,
                timestamp = e.Timestamp.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "focusModeStateChanged",
                Payload = payload
            };

            _logger.LogInformation("Calling _ipcServer.SendEventAsync for focusModeStateChanged");
            await _ipcServer.SendEventAsync(ipcEvent);
            _logger.LogInformation("SendEventAsync completed for focusModeStateChanged");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting FocusModeStateChanged event");
        }
    }
}
