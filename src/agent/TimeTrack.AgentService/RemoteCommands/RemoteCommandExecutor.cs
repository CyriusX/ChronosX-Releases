using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.Services;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Services;
using TimeTrack.AgentService.Ipc;
using TimeTrack.AgentService.Notifications;

namespace TimeTrack.AgentService.RemoteCommands;

/// <summary>
/// Executes remote commands received from the backend admin panel.
/// Each command type maps to a local agent action.
/// </summary>
public sealed class RemoteCommandExecutor : IRemoteCommandExecutor
{
    private readonly TrackingControlUseCase _trackingControl;
    private readonly IAppCategorySyncService _categorySyncService;
    private readonly IIpcServer _ipcServer;
    private readonly IpcNotificationService _notificationService;
    private readonly IHostApplicationLifetime _hostLifetime;
    private readonly ILogger<RemoteCommandExecutor> _logger;

    public RemoteCommandExecutor(
        TrackingControlUseCase trackingControl,
        IAppCategorySyncService categorySyncService,
        IIpcServer ipcServer,
        IpcNotificationService notificationService,
        IHostApplicationLifetime hostLifetime,
        ILogger<RemoteCommandExecutor> logger)
    {
        _trackingControl = trackingControl;
        _categorySyncService = categorySyncService;
        _ipcServer = ipcServer;
        _notificationService = notificationService;
        _hostLifetime = hostLifetime;
        _logger = logger;
    }

    public async Task<CommandResult> ExecuteAsync(string commandType, string? payloadJson, CancellationToken ct)
    {
        return commandType switch
        {
            "stop_tracking" => await ExecuteStopTrackingAsync(ct),
            "resume_tracking" => await ExecuteResumeTrackingAsync(ct),
            "force_sync" => await ExecuteForceSyncAsync(ct),
            "send_notification" => await ExecuteSendNotificationAsync(payloadJson, ct),
            "restart" => await ExecuteRestartAsync(ct),
            _ => CommandResult.Failed($"Unknown command type: {commandType}")
        };
    }

    private async Task<CommandResult> ExecuteStopTrackingAsync(CancellationToken ct)
    {
        await _trackingControl.StopAsync(new StopTrackingRequest
        {
            StoppedBy = "RemoteAdmin",
            Reason = "Comando remoto do administrador"
        }, ct);

        if (_ipcServer.IsClientConnected)
        {
            await _ipcServer.SendEventAsync(new IpcEvent
            {
                EventType = "trackingStateChanged",
                Payload = new { isTracking = false, isPaused = false }
            }, ct);
        }

        _logger.LogInformation("Tracking stopped by remote admin command");
        return CommandResult.Ok("Tracking stopped");
    }

    private async Task<CommandResult> ExecuteResumeTrackingAsync(CancellationToken ct)
    {
        await _trackingControl.StartAsync(new StartTrackingRequest
        {
            StartedBy = "RemoteAdmin"
        }, ct);

        if (_ipcServer.IsClientConnected)
        {
            await _ipcServer.SendEventAsync(new IpcEvent
            {
                EventType = "trackingStateChanged",
                Payload = new { isTracking = true, isPaused = false }
            }, ct);
        }

        _logger.LogInformation("Tracking resumed by remote admin command");
        return CommandResult.Ok("Tracking resumed");
    }

    private async Task<CommandResult> ExecuteForceSyncAsync(CancellationToken ct)
    {
        var success = await _categorySyncService.ForceSyncAsync(ct);
        _logger.LogInformation("Force sync executed, result: {Success}", success);
        return success
            ? CommandResult.Ok("Sync completed")
            : CommandResult.Failed("Sync failed");
    }

    private async Task<CommandResult> ExecuteSendNotificationAsync(string? payloadJson, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(payloadJson))
            return CommandResult.Failed("Notification payload is required");

        try
        {
            var payload = JsonSerializer.Deserialize<NotificationPayload>(payloadJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (payload == null || string.IsNullOrEmpty(payload.Title))
                return CommandResult.Failed("Invalid notification payload");

            if (_ipcServer.IsClientConnected)
            {
                await _ipcServer.SendEventAsync(new IpcEvent
                {
                    EventType = "showNotification",
                    Payload = new
                    {
                        title = payload.Title,
                        body = payload.Body ?? "",
                        kind = "generic",
                        tag = "remote-notification"
                    }
                }, ct);
            }

            _logger.LogInformation("Remote notification sent: {Title}", payload.Title);
            return CommandResult.Ok("Notification sent");
        }
        catch (Exception ex)
        {
            return CommandResult.Failed($"Failed to send notification: {ex.Message}");
        }
    }

    private Task<CommandResult> ExecuteRestartAsync(CancellationToken ct)
    {
        _logger.LogWarning("Agent restart requested by remote admin command. Shutting down...");

        // StopApplication triggers graceful shutdown.
        // Windows Service Controller will restart the service if configured with recovery options.
        // The ack is sent by RemoteCommandService BEFORE this method returns to the caller,
        // because ExecuteAsync returns the result, then the caller acks, then we schedule the stop.
        Task.Run(async () =>
        {
            await Task.Delay(2000, CancellationToken.None); // Give time for ack to be sent
            _hostLifetime.StopApplication();
        });

        return Task.FromResult(CommandResult.Ok("Restart initiated"));
    }

    private sealed class NotificationPayload
    {
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
    }
}
