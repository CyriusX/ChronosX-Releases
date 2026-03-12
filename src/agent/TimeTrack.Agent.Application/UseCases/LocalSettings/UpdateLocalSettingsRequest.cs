namespace TimeTrack.Agent.Application.UseCases.LocalSettings;

/// <summary>
/// Request para atualização parcial das configurações locais
/// </summary>
public sealed class UpdateLocalSettingsRequest
{
    /// <summary>
    /// Novo valor para toggle de auto-resume (null = não alterar)
    /// </summary>
    public bool? AutoResumeNotificationEnabled { get; init; }

    /// <summary>
    /// Novo valor para toggle de sons (null = não alterar)
    /// </summary>
    public bool? NotificationSoundsEnabled { get; init; }

    /// <summary>
    /// Novo idioma (null = não alterar)
    /// </summary>
    public string? Language { get; init; }
}
