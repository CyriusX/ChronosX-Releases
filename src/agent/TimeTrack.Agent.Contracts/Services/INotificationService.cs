using TimeTrack.Agent.Contracts.Notifications;

namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Interface para envio de notificações ao usuário
///
/// SOLID:
/// - SRP: Apenas envia notificações
/// - ISP: Interface coesa com métodos relacionados
/// - DIP: Permite injeção de diferentes implementações (Windows, Mock, etc.)
///
/// Implementações:
/// - WindowsToastNotificationService: Toast nativo Windows 10/11
/// - MockNotificationService: Para testes unitários
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Envia uma notificação ao usuário
    /// </summary>
    /// <param name="notification">Dados da notificação</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    /// <returns>True se a notificação foi enviada com sucesso</returns>
    Task<bool> SendAsync(AgentNotification notification, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela/remove uma notificação específica por tag
    /// </summary>
    /// <param name="tag">Tag da notificação</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task ClearAsync(string tag, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancela todas as notificações do aplicativo
    /// </summary>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task ClearAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Evento disparado quando o usuário interage com uma ação da notificação
    /// </summary>
    event EventHandler<NotificationActionEventArgs>? ActionInvoked;
}

/// <summary>
/// Argumentos do evento de ação de notificação
///
/// SRP: Apenas transporta dados da ação invocada
/// </summary>
public sealed class NotificationActionEventArgs : EventArgs
{
    /// <summary>
    /// Comando IPC associado à ação
    /// </summary>
    public string IpcCommand { get; init; } = string.Empty;

    /// <summary>
    /// Argumentos do comando
    /// </summary>
    public string? Arguments { get; init; }

    /// <summary>
    /// Tag da notificação que originou a ação
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Timestamp da interação
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
