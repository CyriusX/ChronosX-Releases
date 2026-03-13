using Microsoft.Extensions.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.DesktopHost.Ipc;

namespace TimeTrack.DesktopHost.Notifications;

/// <summary>
/// Handles toast notification activation (button clicks)
///
/// SRP: Apenas processa ações de ativação de toast
/// OCP: Pode ser estendido para novos tipos de argumentos
/// </summary>
public sealed class ToastActivationHandler : IDisposable
{
    private readonly IIpcClient _ipcClient;
    private readonly ILogger<ToastActivationHandler> _logger;
    private bool _subscribed;
    private bool _disposed;

    /// <summary>
    /// Event raised when a toast action is invoked
    /// </summary>
    public event EventHandler<NotificationActionEventArgs>? ActionInvoked;

    public ToastActivationHandler(
        IIpcClient ipcClient,
        ILogger<ToastActivationHandler> logger)
    {
        _ipcClient = ipcClient ?? throw new ArgumentNullException(nameof(ipcClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Subscribe to toast activation events
    /// </summary>
    public void Subscribe()
    {
        if (_subscribed)
            return;

        ToastNotificationManagerCompat.OnActivated += HandleToastActivated;
        _subscribed = true;
        _logger.LogInformation("Toast activation handler subscribed");
    }

    /// <summary>
    /// Unsubscribe from toast activation events
    /// </summary>
    public void Unsubscribe()
    {
        if (!_subscribed)
            return;

        ToastNotificationManagerCompat.OnActivated -= HandleToastActivated;
        _subscribed = false;
        _logger.LogInformation("Toast activation handler unsubscribed");
    }

    private void HandleToastActivated(ToastNotificationActivatedEventArgsCompat e)
    {
        try
        {
            var arguments = e.Argument;

            _logger.LogDebug("Toast activated with arguments: {Arguments}", arguments);

            // Parse arguments format: "command=StartBreak&tag=focus-break"
            var parsedArgs = ParseArguments(arguments);

            if (!parsedArgs.TryGetValue("command", out var command))
            {
                _logger.LogWarning("Toast activation missing 'command' argument: {Arguments}", arguments);
                return;
            }

            // Send IPC command to AgentService (fire-and-forget)
            _ = Task.Run(async () => await SendIpcCommandAsync(command, parsedArgs));

            _logger.LogInformation("Toast action processed: {Command}", command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing toast activation");
        }
    }

    private async Task SendIpcCommandAsync(string command, Dictionary<string, string> args)
    {
        try
        {
            if (!_ipcClient.IsConnected)
            {
                _logger.LogWarning("IPC client not connected, cannot process toast action");
                return;
            }

            object? payload = null;
            if (args.TryGetValue("payload", out var payloadStr) && !string.IsNullOrEmpty(payloadStr))
            {
                payload = payloadStr;
            }

            await _ipcClient.SendCommandAsync(command, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending IPC command for toast action");
        }
    }

    private static Dictionary<string, string> ParseArguments(string? arguments)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(arguments))
            return result;

        // Format: "key1=value1;key2=value2" or "key1=value1&key2=value2"
        var separator = arguments.Contains(';') ? ';' : '&';
        var pairs = arguments.Split(separator);

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=', 2);
            if (keyValue.Length == 2)
            {
                result[keyValue[0].Trim()] = Uri.UnescapeDataString(keyValue[1].Trim());
            }
        }

        return result;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Unsubscribe();
        _disposed = true;
    }
}
