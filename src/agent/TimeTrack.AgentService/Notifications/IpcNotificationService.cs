using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Notifications;

/// <summary>
/// IPC-based implementation of INotificationService
/// Forwards notifications to DesktopHost via IPC for display as Windows toasts
///
/// SOLID:
/// - SRP: Only forwards notifications via IPC
/// - LSP: Can be used where INotificationService is expected
/// - DIP: Depends on IIpcServer abstraction
///
/// CX-139: Integration with Focus Mode notifications
/// </summary>
public sealed class IpcNotificationService : INotificationService
{
    private readonly IIpcServer _ipcServer;
    private readonly ILogger<IpcNotificationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    public IpcNotificationService(
        IIpcServer ipcServer,
        ILogger<IpcNotificationService> logger)
    {
        _ipcServer = ipcServer ?? throw new ArgumentNullException(nameof(ipcServer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task<bool> SendAsync(AgentNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification == null)
            throw new ArgumentNullException(nameof(notification));

        if (!_ipcServer.IsClientConnected)
        {
            _logger.LogWarning("Cannot send notification - no DesktopHost connected: {Title}", notification.Title);
            return false;
        }

        try
        {
            var payload = new
            {
                title = notification.Title,
                body = notification.Body,
                kind = notification.Kind.ToString(),
                tag = notification.Tag,
                correlationId = notification.CorrelationId,
                expiresIn = notification.ExpiresIn?.TotalSeconds,
                primaryAction = notification.PrimaryAction != null ? new
                {
                    label = notification.PrimaryAction.Label,
                    ipcCommand = notification.PrimaryAction.IpcCommand,
                    ipcArguments = notification.PrimaryAction.IpcArguments
                } : null,
                secondaryAction = notification.SecondaryAction != null ? new
                {
                    label = notification.SecondaryAction.Label,
                    ipcCommand = notification.SecondaryAction.IpcCommand,
                    ipcArguments = notification.SecondaryAction.IpcArguments
                } : null
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "showNotification",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);

            _logger.LogInformation(
                "Notification forwarded via IPC: {Title} | Kind: {Kind}",
                notification.Title, notification.Kind);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to forward notification via IPC: {Title}", notification.Title);
            return false;
        }
    }

    public async Task ClearAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty", nameof(tag));

        if (!_ipcServer.IsClientConnected)
        {
            _logger.LogDebug("Cannot clear notification - no DesktopHost connected: {Tag}", tag);
            return;
        }

        try
        {
            var ipcEvent = new IpcEvent
            {
                EventType = "clearNotification",
                Payload = new { tag }
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
            _logger.LogDebug("Clear notification forwarded via IPC: {Tag}", tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to forward clear notification via IPC: {Tag}", tag);
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
        {
            _logger.LogDebug("Cannot clear all notifications - no DesktopHost connected");
            return;
        }

        try
        {
            var ipcEvent = new IpcEvent
            {
                EventType = "clearAllNotifications",
                Payload = new { }
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
            _logger.LogDebug("Clear all notifications forwarded via IPC");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to forward clear all notifications via IPC");
        }
    }

    /// <summary>
    /// Called when DesktopHost receives a notification action from user
    /// This is invoked by NotificationEventHandler when it receives action responses
    /// </summary>
    public void OnActionInvoked(NotificationActionEventArgs args)
    {
        ActionInvoked?.Invoke(this, args);
    }
}
