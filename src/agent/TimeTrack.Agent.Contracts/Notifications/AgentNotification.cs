namespace TimeTrack.Agent.Contracts.Notifications;

/// <summary>
/// Representa uma notificação a ser exibida para o usuário
///
/// SRP: Apenas encapsula dados da notificação
/// </summary>
public sealed record AgentNotification
{
    /// <summary>
    /// Título da notificação
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Corpo/mensagem da notificação
    /// </summary>
    public string Body { get; init; }

    /// <summary>
    /// Tipo/categoria da notificação
    /// </summary>
    public NotificationKind Kind { get; init; }

    /// <summary>
    /// Ação primária (ex: "Iniciar pausa")
    /// </summary>
    public NotificationAction? PrimaryAction { get; init; }

    /// <summary>
    /// Ação secundária (ex: "Pular pausa")
    /// </summary>
    public NotificationAction? SecondaryAction { get; init; }

    /// <summary>
    /// Tempo para auto-dismiss da notificação
    /// Null = usa default do Windows
    /// </summary>
    public TimeSpan? ExpiresIn { get; init; }

    /// <summary>
    /// Tag para agrupamento de notificações (notificações com mesma tag se substituem)
    /// </summary>
    public string? Tag { get; init; }

    /// <summary>
    /// Identificador único da notificação para rastreamento
    /// </summary>
    public string? CorrelationId { get; init; }

    public AgentNotification(
        string title,
        string body,
        NotificationKind kind = NotificationKind.Generic)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title cannot be empty", nameof(title));

        if (string.IsNullOrWhiteSpace(body))
            throw new ArgumentException("Body cannot be empty", nameof(body));

        Title = title;
        Body = body;
        Kind = kind;
    }

    // Factory methods para criar notificações comuns

    /// <summary>
    /// Cria notificação de pausa do Pomodoro
    /// </summary>
    public static AgentNotification FocusBreakNotification(
        string title,
        string body,
        NotificationAction primaryAction,
        NotificationAction? secondaryAction = null)
    {
        return new AgentNotification(title, body, NotificationKind.FocusBreak)
        {
            PrimaryAction = primaryAction,
            SecondaryAction = secondaryAction,
            Tag = "focus-break",
            ExpiresIn = TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Cria notificação de retomada do Pomodoro
    /// </summary>
    public static AgentNotification FocusResumeNotification(
        string title,
        string body,
        NotificationAction primaryAction,
        NotificationAction? secondaryAction = null)
    {
        return new AgentNotification(title, body, NotificationKind.FocusResume)
        {
            PrimaryAction = primaryAction,
            SecondaryAction = secondaryAction,
            Tag = "focus-resume",
            ExpiresIn = TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Cria notificação de queda Ultradian
    /// </summary>
    public static AgentNotification UltradianDipNotification(
        string title,
        string body,
        NotificationAction primaryAction,
        NotificationAction? secondaryAction = null)
    {
        return new AgentNotification(title, body, NotificationKind.UltradianDip)
        {
            PrimaryAction = primaryAction,
            SecondaryAction = secondaryAction,
            Tag = "ultradian-dip",
            ExpiresIn = TimeSpan.FromMinutes(10)
        };
    }
}
