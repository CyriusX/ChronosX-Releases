namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Serviço para registro de eventos do agent (ações do usuário, eventos de sistema, erros).
/// Eventos são persistidos localmente e sincronizados com o backend via outbox.
/// </summary>
public interface IAgentEventLogger
{
    /// <summary>
    /// Registra um evento do agent.
    /// Fire-and-forget: nunca bloqueia o chamador.
    /// </summary>
    /// <param name="eventType">Tipo do evento (ex: "tracking.started", "sync.failed")</param>
    /// <param name="category">Categoria: "user_action", "system", "error"</param>
    /// <param name="severity">Severidade: "info", "warning", "error", "critical"</param>
    /// <param name="message">Mensagem descritiva</param>
    /// <param name="metadata">Dados adicionais (será serializado como JSON)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    Task LogAsync(
        string eventType,
        string category,
        string severity,
        string message,
        object? metadata = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Categorias de eventos do agent
/// </summary>
public static class AgentEventCategory
{
    public const string UserAction = "user_action";
    public const string System = "system";
    public const string Error = "error";
}

/// <summary>
/// Severidades de eventos
/// </summary>
public static class AgentEventSeverity
{
    public const string Info = "info";
    public const string Warning = "warning";
    public const string Error = "error";
    public const string Critical = "critical";
}
