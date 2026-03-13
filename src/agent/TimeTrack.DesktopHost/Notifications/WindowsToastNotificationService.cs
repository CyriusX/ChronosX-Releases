using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using TimeTrack.Agent.Contracts.Notifications;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.DesktopHost.Notifications;

/// <summary>
/// Implementation of INotificationService using Windows Toast Notifications
///
/// SOLID:
/// - SRP: Apenas orquestra a criação e exibição de toast notifications
/// - OCP: ToastContentBuilder permite extensão para novos tipos
/// - LSP: Pode ser substituído por outras implementações de INotificationService
/// - ISP: Implementa apenas a interface INotificationService
/// - DIP: Depende de ILogger (abstração)
///
/// Requirements:
/// - Windows 10 version 1903+ or Windows 11
/// - Microsoft.Toolkit.Uwp.Notifications NuGet package
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsToastNotificationService : INotificationService, IDisposable
{
    private readonly ToastActivationHandler _activationHandler;
    private readonly ILogger<WindowsToastNotificationService> _logger;
    private bool _disposed;

    /// <inheritdoc />
    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    public WindowsToastNotificationService(
        ToastActivationHandler activationHandler,
        ILogger<WindowsToastNotificationService> logger)
    {
        _activationHandler = activationHandler ?? throw new ArgumentNullException(nameof(activationHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Subscribe to activation events
        _activationHandler.Subscribe();

        _logger.LogInformation("Windows Toast Notification Service initialized");
    }

    /// <inheritdoc />
    public Task<bool> SendAsync(AgentNotification notification, CancellationToken cancellationToken = default)
    {
        if (notification == null)
            throw new ArgumentNullException(nameof(notification));

        try
        {
            var builder = CreateToastBuilder(notification);

            // Show the toast using the builder's Show method
            builder.Show();

            _logger.LogInformation(
                "Toast notification sent: {Title} | Kind: {Kind} | Tag: {Tag}",
                notification.Title, notification.Kind, notification.Tag);

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send toast notification: {Title}", notification.Title);
            return Task.FromResult(false);
        }
    }

    /// <inheritdoc />
    public Task ClearAsync(string tag, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tag))
            throw new ArgumentException("Tag cannot be empty", nameof(tag));

        try
        {
            ToastNotificationManagerCompat.History.Remove(tag, "TimeTrack");
            _logger.LogDebug("Cleared toast notification with tag: {Tag}", tag);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear toast notification with tag: {Tag}", tag);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ToastNotificationManagerCompat.History.Clear();
            _logger.LogInformation("Cleared all toast notifications");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clear all toast notifications");
        }

        return Task.CompletedTask;
    }

    private ToastContentBuilder CreateToastBuilder(AgentNotification notification)
    {
        var builder = new ToastContentBuilder()
            .SetToastScenario(ToastScenario.Default)
            .AddHeader(
                id: "timetrack-header",
                title: "TimeTrack",
                arguments: "app=TimeTrack")
            .AddText(notification.Title)
            .AddText(notification.Body);

        // Add icon and attribution based on notification kind
        builder = notification.Kind switch
        {
            NotificationKind.FocusBreak => AddFocusBreakVisuals(builder),
            NotificationKind.FocusResume => AddFocusResumeVisuals(builder),
            NotificationKind.UltradianDip => AddUltradianDipVisuals(builder),
            _ => AddGenericVisuals(builder)
        };

        // Add primary action button
        if (notification.PrimaryAction is not null)
        {
            var button = new ToastButton()
                .SetContent(notification.PrimaryAction.Label)
                .AddArgument("command", notification.PrimaryAction.IpcCommand)
                .AddArgument("tag", notification.Tag ?? Guid.NewGuid().ToString());

            if (!string.IsNullOrEmpty(notification.PrimaryAction.IpcArguments))
            {
                button = button.AddArgument("payload", notification.PrimaryAction.IpcArguments);
            }

            builder.AddButton(button);
        }

        // Add secondary action button
        if (notification.SecondaryAction is not null)
        {
            var button = new ToastButton()
                .SetContent(notification.SecondaryAction.Label)
                .AddArgument("command", notification.SecondaryAction.IpcCommand)
                .AddArgument("tag", notification.Tag ?? Guid.NewGuid().ToString());

            if (!string.IsNullOrEmpty(notification.SecondaryAction.IpcArguments))
            {
                button = button.AddArgument("payload", notification.SecondaryAction.IpcArguments);
            }

            builder.AddButton(button);
        }

        // Add timestamp
        builder.AddCustomTimeStamp(DateTime.Now);

        return builder;
    }

    #region Visual Builders - SRP: Each method builds specific visual elements

    private static ToastContentBuilder AddFocusBreakVisuals(ToastContentBuilder builder)
    {
        // Pomodoro break - use tomato-like styling
        return builder
            .AddAppLogoOverride(new Uri("file:///C:/Windows/System32/@WiseWin.dll,-101"))
            .AddAttributionText("Pomodoro - Hora de pausar");
    }

    private static ToastContentBuilder AddFocusResumeVisuals(ToastContentBuilder builder)
    {
        // Pomodoro resume
        return builder
            .AddAttributionText("Pomodoro - Hora de retomar");
    }

    private static ToastContentBuilder AddUltradianDipVisuals(ToastContentBuilder builder)
    {
        // Ultradian rhythm dip
        return builder
            .AddAttributionText("Ciclo Ultradian - Pausa sugerida");
    }

    private static ToastContentBuilder AddGenericVisuals(ToastContentBuilder builder)
    {
        return builder
            .AddAttributionText("TimeTrack Agent");
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
            return;

        _activationHandler.Unsubscribe();
        _disposed = true;

        _logger.LogDebug("Windows Toast Notification Service disposed");
    }
}
