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
    private readonly IUpdateService _updateService;
    private readonly ILogger<RemoteCommandExecutor> _logger;

    public RemoteCommandExecutor(
        TrackingControlUseCase trackingControl,
        IAppCategorySyncService categorySyncService,
        IIpcServer ipcServer,
        IpcNotificationService notificationService,
        IHostApplicationLifetime hostLifetime,
        IUpdateService updateService,
        ILogger<RemoteCommandExecutor> logger)
    {
        _trackingControl = trackingControl;
        _categorySyncService = categorySyncService;
        _ipcServer = ipcServer;
        _notificationService = notificationService;
        _hostLifetime = hostLifetime;
        _updateService = updateService;
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
            "force_update" => await ExecuteForceUpdateAsync(ct),
            "task_assigned" => await ExecuteKanbanNotificationAsync("task_assigned", payloadJson, ct),
            "task_unassigned" => await ExecuteKanbanNotificationAsync("task_unassigned", payloadJson, ct),
            "task_updated" => await ExecuteKanbanNotificationAsync("task_updated", payloadJson, ct),
            "project_membership_changed" => await ExecuteKanbanNotificationAsync("project_membership_changed", payloadJson, ct),
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

        Task.Run(async () =>
        {
            await Task.Delay(2000, CancellationToken.None); // Give time for ack to be sent

            // Start a new instance of ourselves before stopping
            try
            {
                var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                {
                    var startInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = AppContext.BaseDirectory,
                        UseShellExecute = true
                    };
                    System.Diagnostics.Process.Start(startInfo);
                    _logger.LogInformation("New agent process started: {Path}", exePath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start new agent process");
            }

            _hostLifetime.StopApplication();
        });

        return Task.FromResult(CommandResult.Ok("Restart initiated"));
    }

    /// <summary>
    /// Handles kanban notification commands pushed by the backend
    /// (task_assigned, task_unassigned, task_updated, project_membership_changed).
    /// Wraps the server-supplied notification payload and forwards it to the
    /// desktop UI as a "showNotification" IPC event plus a "notificationReceived"
    /// event so the bell icon refreshes.
    /// </summary>
    private async Task<CommandResult> ExecuteKanbanNotificationAsync(string kind, string? payloadJson, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(payloadJson))
            return CommandResult.Failed($"{kind} payload is required");

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;

            // The backend wraps the original metadata under `payload` when queuing.
            var inner = root.TryGetProperty("payload", out var p) ? p : root;

            string? taskTitle = inner.TryGetProperty("taskTitle", out var tt) ? tt.GetString() : null;
            string? projectName = inner.TryGetProperty("projectName", out var pn) ? pn.GetString() : null;
            string? projectColor = inner.TryGetProperty("projectColor", out var pc) ? pc.GetString() : null;
            string? taskId = inner.TryGetProperty("taskId", out var ti) ? ti.GetString() : null;
            string? projectId = inner.TryGetProperty("projectId", out var pi) ? pi.GetString() : null;

            var (title, body) = kind switch
            {
                "task_assigned" => ($"Nova tarefa atribuída",
                                    !string.IsNullOrEmpty(projectName) && !string.IsNullOrEmpty(taskTitle)
                                        ? $"{projectName} · {taskTitle}"
                                        : (taskTitle ?? "Você tem uma nova tarefa")),
                "task_unassigned" => ("Tarefa removida",
                                      !string.IsNullOrEmpty(projectName) && !string.IsNullOrEmpty(taskTitle)
                                          ? $"{projectName} · {taskTitle}"
                                          : (taskTitle ?? "Tarefa foi reatribuída")),
                "task_updated" => ("Tarefa atualizada",
                                   !string.IsNullOrEmpty(projectName) && !string.IsNullOrEmpty(taskTitle)
                                       ? $"{projectName} · {taskTitle}"
                                       : (taskTitle ?? "Detalhes atualizados")),
                "project_membership_changed" => ("Participação em projeto",
                                                 projectName ?? "Seus acessos de projeto mudaram"),
                _ => ("Notificação", "")
            };

            if (_ipcServer.IsClientConnected)
            {
                // Visual toast in the desktop app
                await _ipcServer.SendEventAsync(new IpcEvent
                {
                    EventType = "showNotification",
                    Payload = new
                    {
                        title,
                        body,
                        kind,
                        tag = $"kanban-{kind}-{taskId ?? projectId ?? Guid.NewGuid().ToString()}",
                        taskId,
                        projectId,
                        projectColor,
                    }
                }, ct);

                // Signal the NotificationsBell dropdown to refetch the inbox
                await _ipcServer.SendEventAsync(new IpcEvent
                {
                    EventType = "notificationReceived",
                    Payload = new { kind, taskId, projectId }
                }, ct);

                // Tell MyTasksWidget to refetch assigned tasks
                if (kind is "task_assigned" or "task_unassigned" or "task_updated")
                {
                    await _ipcServer.SendEventAsync(new IpcEvent
                    {
                        EventType = "myTasksChanged",
                        Payload = new { kind, taskId }
                    }, ct);
                }
            }

            _logger.LogInformation("Kanban notification dispatched: {Kind} — {Title}", kind, title);
            return CommandResult.Ok($"{kind} processed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {Kind} payload", kind);
            return CommandResult.Failed($"Failed to process {kind}: {ex.Message}");
        }
    }

    private async Task<CommandResult> ExecuteForceUpdateAsync(CancellationToken ct)
    {
        if (_updateService.IsUpdating)
            return CommandResult.Failed("Update already in progress");

        _logger.LogInformation("Force update requested by remote admin command");

        // Fire-and-forget: check → download → install. The command ack is immediate.
        _ = Task.Run(async () =>
        {
            try
            {
                var checkResult = await _updateService.CheckForUpdatesAsync(CancellationToken.None);
                if (checkResult?.HasUpdate == true)
                {
                    _logger.LogInformation("Remote update: version {Version} available, starting download", checkResult.LatestVersion);
                    await _updateService.StartUpdateAsync(CancellationToken.None);
                }
                else
                {
                    _logger.LogInformation("Remote update: no update available");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Remote force update failed");
            }
        }, CancellationToken.None);

        return CommandResult.Ok("Force update initiated");
    }

    private sealed class NotificationPayload
    {
        public string Title { get; set; } = string.Empty;
        public string? Body { get; set; }
    }
}
