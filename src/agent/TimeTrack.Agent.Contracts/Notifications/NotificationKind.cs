namespace TimeTrack.Agent.Contracts.Notifications;

/// <summary>
/// Define os tipos de notificação do Agent
///
/// SRP: Apenas categoriza o tipo da notificação
/// </summary>
public enum NotificationKind
{
    /// <summary>
    /// Pomodoro: hora de fazer uma pausa
    /// </summary>
    FocusBreak,

    /// <summary>
    /// Pomodoro: pausa concluída, hora de retomar
    /// </summary>
    FocusResume,

    /// <summary>
    /// Ultradian: queda de performance detectada após ~90 min
    /// </summary>
    UltradianDip,

    /// <summary>
    /// Notificação genérica do sistema
    /// </summary>
    Generic
}
