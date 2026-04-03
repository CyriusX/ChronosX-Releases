using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.DesktopHost.Ipc;

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

    public void Dispose()
    {
        if (_disposed)
            return;

        _ipcClient.EventReceived -= OnIpcEventReceived;
        _disposed = true;
    }
}
