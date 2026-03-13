using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Notifications;

/// <summary>
/// Null implementation of INotificationService for AgentService
///
/// SOLID:
/// - SRP: Apenas loga notificações sem exibi-las
/// - LSP: Pode ser usado onde INotificationService é esperado
/// - DIP: Permite AgentService funcionar sem dependência de UI
///
/// Note: In production, this should be replaced with IpcNotificationService
/// that forwards notifications to DesktopHost via IPC for display.
/// </summary>
public sealed class NullNotificationService : INotificationService
{
    private readonly ILogger<NullNotificationService> _logger;

    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    public NullNotificationService(ILogger<NullNotificationService> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(AgentNotification notification, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Notification: {Title} - {Body} (Kind={Kind}, Tag={Tag})",
            notification.Title,
            notification.Body,
            notification.Kind,
            notification.Tag);

        // Return success - notification was "processed" (logged)
        return Task.FromResult(true);
    }

    public Task ClearAsync(string tag, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Clear notification requested for tag: {Tag}", tag);
        return Task.CompletedTask;
    }

    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Clear all notifications requested");
        return Task.CompletedTask;
    }
}
