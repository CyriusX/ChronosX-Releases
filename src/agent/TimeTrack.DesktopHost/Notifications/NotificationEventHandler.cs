using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.DesktopHost.Ipc;
using TimeTrack.DesktopHost.UI;

namespace TimeTrack.DesktopHost.Notifications;

/// <summary>
/// Handles notification events from AgentService and displays Windows toasts
///
/// Listens for IPC events:
/// - showNotification: Display a toast notification
/// - clearNotification: Remove a specific notification by tag
/// - clearAllNotifications: Remove all notifications
///
/// SOLID:
/// - SRP: Only handles notification event routing
/// - DIP: Depends on IIpcClient and INotificationService abstractions
///
/// CX-139: Integration with Focus Mode notifications
/// </summary>
public sealed class NotificationEventHandler : IHostedService, IDisposable
{
    private readonly IIpcClient _ipcClient;
    private readonly INotificationService _notificationService;
    private readonly ILogger<NotificationEventHandler> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private bool _disposed;
    private ActivityResumeToastForm? _activeResumeToast;
    private ActivityResumeToastForm? _activeTaskResumeToast;
    private IdleJustificationPromptForm? _activeIdleJustificationPrompt;

    public NotificationEventHandler(
        IIpcClient ipcClient,
        INotificationService notificationService,
        ILogger<NotificationEventHandler> logger)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ipcClient.EventReceived += OnIpcEventReceived;
        _logger.LogInformation("NotificationEventHandler started - listening for notification events");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _ipcClient.EventReceived -= OnIpcEventReceived;
        _logger.LogInformation("NotificationEventHandler stopped");
        return Task.CompletedTask;
    }

    private async void OnIpcEventReceived(object? sender, IpcEventArgs e)
    {
        try
        {
            switch (e.EventType)
            {
                case "showNotification":
                    await HandleShowNotificationAsync(e.Payload);
                    break;

                case "clearNotification":
                    await HandleClearNotificationAsync(e.Payload);
                    break;

                case "clearAllNotifications":
                    await HandleClearAllNotificationsAsync();
                    break;

                case "showActivityResumePrompt":
                    HandleActivityResumePrompt(e.Payload);
                    break;

                case "showTaskResumePrompt":
                    HandleTaskResumePrompt(e.Payload);
                    break;

                case "showIdleJustificationPrompt":
                    HandleIdleJustificationPrompt(e.Payload);
                    break;

                case "updateAvailable":
                    HandleUpdateAvailableAsync(e.Payload);
                    break;

                case "updateProgress":
                    HandleUpdateProgress(e.Payload);
                    break;

                case "updateComplete":
                    HandleUpdateComplete(e.Payload);
                    break;

                case "updateFailed":
                    HandleUpdateFailed(e.Payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling notification event: {EventType}", e.EventType);
        }
    }

    private async Task HandleShowNotificationAsync(JsonElement payload)
    {
        // Remote admin notifications: bypass AgentNotification validation (body may be empty)
        // and show a centered modal instead of a toast.
        if (payload.TryGetProperty("tag", out var tagEl) && tagEl.GetString() == "remote-notification")
        {
            var title = payload.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var body = payload.TryGetProperty("body", out var b) ? b.GetString() ?? string.Empty : string.Empty;
            _logger.LogInformation("Showing remote admin notification modal: {Title}", title);
            ShowRemoteNotificationModal(title, body);
            return;
        }

        var notification = ParseNotification(payload);
        if (notification == null)
        {
            _logger.LogWarning("Failed to parse notification payload");
            return;
        }

        _logger.LogInformation(
            "Showing notification: {Title} | Kind: {Kind} | Tag: {Tag}",
            notification.Title, notification.Kind, notification.Tag);

        await _notificationService.SendAsync(notification);
    }

    private void ShowRemoteNotificationModal(string title, string body)
    {
        if (System.Windows.Forms.Application.OpenForms.Count > 0)
        {
            var mainForm = System.Windows.Forms.Application.OpenForms[0];
            mainForm?.BeginInvoke(() =>
            {
                var form = new RemoteNotificationForm(title, body);
                form.Show();
            });
        }
        else
        {
            _logger.LogWarning("No open forms — cannot show remote notification modal");
        }
    }

    private async Task HandleClearNotificationAsync(JsonElement payload)
    {
        if (payload.TryGetProperty("tag", out var tagEl))
        {
            var tag = tagEl.GetString();
            if (!string.IsNullOrEmpty(tag))
            {
                _logger.LogDebug("Clearing notification with tag: {Tag}", tag);
                await _notificationService.ClearAsync(tag);
            }
        }
    }

    private async Task HandleClearAllNotificationsAsync()
    {
        _logger.LogDebug("Clearing all notifications");
        await _notificationService.ClearAllAsync();
    }

    private void HandleActivityResumePrompt(JsonElement payload)
    {
        var countdownSeconds = 10;
        if (payload.TryGetProperty("countdownSeconds", out var cdEl) && cdEl.ValueKind == JsonValueKind.Number)
            countdownSeconds = cdEl.GetInt32();

        _logger.LogInformation("Received activity resume prompt — showing toast with {Countdown}s countdown", countdownSeconds);

        // Must show on UI thread
        if (System.Windows.Forms.Application.OpenForms.Count > 0)
        {
            var mainForm = System.Windows.Forms.Application.OpenForms[0];
            mainForm?.BeginInvoke(() => ShowActivityResumeToast(countdownSeconds));
        }
        else
        {
            _logger.LogWarning("No open forms — cannot show activity resume toast");
        }
    }

    private void ShowActivityResumeToast(int countdownSeconds)
    {
        // Prevent duplicate toasts
        if (_activeResumeToast is { Visible: true })
        {
            _logger.LogDebug("Activity resume toast already showing — skipping");
            return;
        }

        var toast = new ActivityResumeToastForm(countdownSeconds);
        _activeResumeToast = toast;

        toast.PromptResult += async (_, resume) =>
        {
            _activeResumeToast = null;
            try
            {
                if (resume)
                {
                    _logger.LogInformation("User accepted activity resume — sending resumeTracking command");
                    await _ipcClient.SendCommandAsync("resumeTracking");
                }
                else
                {
                    _logger.LogInformation("User dismissed activity resume — sending dismissActivityResumePrompt command");
                    await _ipcClient.SendCommandAsync("dismissActivityResumePrompt");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending IPC command after activity resume prompt");
            }
        };

        toast.Show();
    }

    private void HandleTaskResumePrompt(JsonElement payload)
    {
        var countdownSeconds = 30;
        if (payload.TryGetProperty("countdownSeconds", out var cdEl) && cdEl.ValueKind == JsonValueKind.Number)
            countdownSeconds = cdEl.GetInt32();

        var taskTitle = payload.TryGetProperty("taskTitle", out var ttEl) ? ttEl.GetString() : null;
        var projectName = payload.TryGetProperty("projectName", out var pnEl) ? pnEl.GetString() : null;

        _logger.LogInformation(
            "Received task resume prompt — showing toast for task {Task} (countdown {Countdown}s)",
            taskTitle, countdownSeconds);

        if (System.Windows.Forms.Application.OpenForms.Count > 0)
        {
            var mainForm = System.Windows.Forms.Application.OpenForms[0];
            mainForm?.BeginInvoke(() => ShowTaskResumeToast(countdownSeconds, projectName, taskTitle));
        }
        else
        {
            _logger.LogWarning("No open forms — cannot show task resume toast");
        }
    }

    private void ShowTaskResumeToast(int countdownSeconds, string? projectName, string? taskTitle)
    {
        if (_activeTaskResumeToast is { Visible: true })
        {
            _logger.LogDebug("Task resume toast already showing — skipping");
            return;
        }

        var subtitle = !string.IsNullOrEmpty(taskTitle) && !string.IsNullOrEmpty(projectName)
            ? $"{projectName} · {taskTitle}"
            : (taskTitle ?? "Tarefa em andamento");

        var toast = new ActivityResumeToastForm(
            countdownSeconds,
            titleOverride: "Ainda trabalhando nessa tarefa?",
            subtitleOverride: subtitle,
            yesButtonOverride: "\u25B6   Continuar",
            noButtonOverride: "Encerrar tarefa");

        _activeTaskResumeToast = toast;

        toast.PromptResult += async (_, resume) =>
        {
            _activeTaskResumeToast = null;
            try
            {
                if (resume)
                {
                    _logger.LogInformation("User accepted task resume — sending ResumeOpenTask command");
                    await _ipcClient.SendCommandAsync("ResumeOpenTask");
                }
                else
                {
                    _logger.LogInformation("User rejected task resume — sending CloseOpenTask command");
                    await _ipcClient.SendCommandAsync("CloseOpenTask");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending IPC command after task resume prompt");
            }
        };

        toast.Show();
    }

    private void HandleIdleJustificationPrompt(JsonElement payload)
    {
        var idlePeriodId = payload.TryGetProperty("idlePeriodId", out var idEl) ? idEl.GetString() : null;
        var startedAt = payload.TryGetProperty("startedAt", out var startEl) &&
                        DateTime.TryParse(startEl.GetString(), out var parsedStart)
            ? parsedStart
            : DateTime.UtcNow;
        var endedAt = payload.TryGetProperty("endedAt", out var endEl) &&
                      DateTime.TryParse(endEl.GetString(), out var parsedEnd)
            ? parsedEnd
            : DateTime.UtcNow;
        var durationSeconds = payload.TryGetProperty("durationSeconds", out var durationEl) && durationEl.ValueKind == JsonValueKind.Number
            ? durationEl.GetInt32()
            : (int)Math.Max(0, (endedAt - startedAt).TotalSeconds);

        if (string.IsNullOrWhiteSpace(idlePeriodId))
        {
            _logger.LogWarning("Idle justification prompt ignored because idlePeriodId is missing");
            return;
        }

        if (System.Windows.Forms.Application.OpenForms.Count > 0)
        {
            var mainForm = System.Windows.Forms.Application.OpenForms[0];
            mainForm?.BeginInvoke(() => ShowIdleJustificationPrompt(idlePeriodId, startedAt, endedAt, durationSeconds));
        }
        else
        {
            _logger.LogWarning("No open forms — cannot show idle justification prompt");
        }
    }

    private void ShowIdleJustificationPrompt(string idlePeriodId, DateTime startedAt, DateTime endedAt, int durationSeconds)
    {
        if (_activeIdleJustificationPrompt is { Visible: true })
        {
            _logger.LogDebug("Idle justification prompt already visible — skipping duplicate for {IdlePeriodId}", idlePeriodId);
            return;
        }

        var prompt = new IdleJustificationPromptForm(idlePeriodId, startedAt, endedAt, durationSeconds);
        _activeIdleJustificationPrompt = prompt;

        prompt.Submitted += async (_, args) =>
        {
            _activeIdleJustificationPrompt = null;

            try
            {
                if (args.IsSkipped)
                {
                    await _ipcClient.SendCommandAsync("dismissIdleJustification", new
                    {
                        idlePeriodId = args.IdlePeriodId
                    });
                    return;
                }

                await _ipcClient.SendCommandAsync("submitIdleJustification", new
                {
                    idlePeriodId = args.IdlePeriodId,
                    reasonCode = args.ReasonCode,
                    note = args.Note
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending idle justification command for {IdlePeriodId}", args.IdlePeriodId);
            }
        };

        prompt.Show();
    }

    private AgentNotification? ParseNotification(JsonElement payload)
    {
        try
        {
            if (!payload.TryGetProperty("title", out var titleEl) ||
                !payload.TryGetProperty("body", out var bodyEl))
            {
                return null;
            }

            var title = titleEl.GetString() ?? string.Empty;
            var body = bodyEl.GetString() ?? string.Empty;

            // Parse kind
            var kind = NotificationKind.Generic;
            if (payload.TryGetProperty("kind", out var kindEl))
            {
                var kindStr = kindEl.GetString();
                if (Enum.TryParse<NotificationKind>(kindStr, ignoreCase: true, out var parsedKind))
                {
                    kind = parsedKind;
                }
            }

            var notification = new AgentNotification(title, body, kind);

            // Parse optional fields using reflection since record is immutable
            if (payload.TryGetProperty("tag", out var tagEl) && tagEl.ValueKind != JsonValueKind.Null)
            {
                notification = notification with { Tag = tagEl.GetString() };
            }

            if (payload.TryGetProperty("correlationId", out var corrEl) && corrEl.ValueKind != JsonValueKind.Null)
            {
                notification = notification with { CorrelationId = corrEl.GetString() };
            }

            if (payload.TryGetProperty("expiresIn", out var expiresEl) && expiresEl.ValueKind == JsonValueKind.Number)
            {
                var seconds = expiresEl.GetDouble();
                notification = notification with { ExpiresIn = TimeSpan.FromSeconds(seconds) };
            }

            // Parse primary action
            if (payload.TryGetProperty("primaryAction", out var primaryEl) && primaryEl.ValueKind == JsonValueKind.Object)
            {
                var primaryAction = ParseNotificationAction(primaryEl);
                if (primaryAction != null)
                {
                    notification = notification with { PrimaryAction = primaryAction };
                }
            }

            // Parse secondary action
            if (payload.TryGetProperty("secondaryAction", out var secondaryEl) && secondaryEl.ValueKind == JsonValueKind.Object)
            {
                var secondaryAction = ParseNotificationAction(secondaryEl);
                if (secondaryAction != null)
                {
                    notification = notification with { SecondaryAction = secondaryAction };
                }
            }

            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing notification payload");
            return null;
        }
    }

    private static NotificationAction? ParseNotificationAction(JsonElement element)
    {
        try
        {
            var label = element.TryGetProperty("label", out var labelEl) ? labelEl.GetString() : null;
            var ipcCommand = element.TryGetProperty("ipcCommand", out var cmdEl) ? cmdEl.GetString() : null;

            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(ipcCommand))
            {
                return null;
            }

            var ipcArguments = element.TryGetProperty("ipcArguments", out var argsEl)
                ? argsEl.GetString()
                : null;

            return new NotificationAction(label, ipcCommand, ipcArguments);
        }
        catch
        {
            return null;
        }
    }

    private async Task HandleUpdateAvailableAsync(JsonElement payload)
    {
        var version = payload.TryGetProperty("latestVersion", out var v) ? v.GetString() : "unknown";
        var fileSizeBytes = payload.TryGetProperty("fileSizeBytes", out var size) ? size.GetInt64() : 0;
        var releaseNotes = payload.TryGetProperty("releaseNotes", out var notes) ? notes.GetString() : null;

        var fileSizeMb = fileSizeBytes / (1024.0 * 1024.0);

        _logger.LogInformation(
            "Update available: Version={Version}, Size={Size:F1}MB",
            version, fileSizeMb);

        // Show notification to user (opt-in — user decides when to update)
        var notification = new AgentNotification(
            "Update Available",
            $"A new version ({version}) is available. Open the app to update.",
            NotificationKind.System)
        {
            Tag = "update-available"
        };

        await _notificationService.SendAsync(notification);
    }

    private void HandleUpdateProgress(JsonElement payload)
    {
        var stage = payload.TryGetProperty("stage", out var s) ? s.GetString() : "unknown";
        var percentage = payload.TryGetProperty("percentage", out var p) ? p.GetInt32() : 0;
        var message = payload.TryGetProperty("message", out var m) ? m.GetString() : string.Empty;
        var targetVersion = payload.TryGetProperty("targetVersion", out var v) ? v.GetString() : null;

        _logger.LogDebug(
            "Update progress: Stage={Stage}, Percentage={Percentage}%, Message={Message}",
            stage, percentage, message);

        // Forward to WebView2 UI for UpdateModal display
        ForwardUpdateProgressToWebView(stage, percentage, message, targetVersion);
    }

    private void ForwardUpdateProgressToWebView(string? stage, int percentage, string? message, string? targetVersion)
    {
        if (System.Windows.Forms.Application.OpenForms.Count > 0)
        {
            var mainForm = System.Windows.Forms.Application.OpenForms[0];
            mainForm?.BeginInvoke(() =>
            {
                // Notify WebView2 to show/update the update modal
                // This is handled by MainForm which forwards to WebView2
                if (mainForm is UI.MainForm form)
                {
                    form.ShowUpdateProgress(stage, percentage, message, targetVersion);
                }
            });
        }
    }

    private void HandleUpdateComplete(JsonElement payload)
    {
        var version = payload.TryGetProperty("version", out var v) ? v.GetString() : "unknown";
        var restartRequired = payload.TryGetProperty("restartRequired", out var r) && r.GetBoolean();

        _logger.LogInformation(
            "Update complete: Version={Version}, RestartRequired={RestartRequired}",
            version, restartRequired);

        // Show completion notification
        _ = Task.Run(async () =>
        {
            var notification = new AgentNotification(
                "Update Complete",
                $"ChronosX has been updated to version {version}. The application will restart.",
                NotificationKind.System)
            {
                Tag = "update-complete"
            };

            await _notificationService.SendAsync(notification);

            // Restart the application after a short delay
            await Task.Delay(2000);
            RestartApplication();
        });
    }

    private void HandleUpdateFailed(JsonElement payload)
    {
        var error = payload.TryGetProperty("error", out var e) ? e.GetString() : "Unknown error";
        var canRollback = payload.TryGetProperty("canRollback", out var rb) && rb.GetBoolean();

        _logger.LogError(
            "Update failed: Error={Error}, CanRollback={CanRollback}",
            error, canRollback);

        // Show error notification
        _ = Task.Run(async () =>
        {
            var notification = new AgentNotification(
                "Update Failed",
                $"Failed to update: {error}. {(canRollback ? "Attempting rollback..." : "Please try again later.")}",
                NotificationKind.Error)
            {
                Tag = "update-failed"
            };

            await _notificationService.SendAsync(notification);
        });
    }

    private static void RestartApplication()
    {
        if (System.Windows.Forms.Application.OpenForms.Count > 0)
        {
            var mainForm = System.Windows.Forms.Application.OpenForms[0];
            mainForm?.BeginInvoke(() =>
            {
                System.Windows.Forms.Application.Restart();
            });
        }
        else
        {
            System.Windows.Forms.Application.Restart();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _ipcClient.EventReceived -= OnIpcEventReceived;
        _disposed = true;
    }
}
