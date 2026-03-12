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
    /// Timestamp da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}
