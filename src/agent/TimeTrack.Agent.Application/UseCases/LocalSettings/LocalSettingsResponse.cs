namespace TimeTrack.Agent.Application.UseCases.LocalSettings;

/// <summary>
/// Response com as configurações locais do colaborador
/// </summary>
public sealed class LocalSettingsResponse
{
    /// <summary>
    /// Toggle para notificação de auto-resume
    /// </summary>
    public bool AutoResumeNotificationEnabled { get; init; } = true;

    /// <summary>
    /// Toggle para sons de notificação
    /// </summary>
    public bool NotificationSoundsEnabled { get; init; } = true;

    /// <summary>
    /// Idioma da interface (pt-BR, en-US)
    /// </summary>
    public string Language { get; init; } = "pt-BR";

    /// <summary>
    /// Limiar de inatividade em segundos (null = usar padrão do Agent)
    /// </summary>
    public int? IdleThresholdSeconds { get; init; }

    /// <summary>
    /// Meta diária de trabalho em segundos (null = padrão 28800 = 8h)
    /// </summary>
    public int? WorkGoalSeconds { get; init; }

    /// <summary>
    /// Timestamp da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
