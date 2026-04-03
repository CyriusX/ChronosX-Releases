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

    /// <summary>
    /// Novo limiar de inatividade em segundos (null = não alterar, 60–3600)
    /// </summary>
    public int? IdleThresholdSeconds { get; init; }

    /// <summary>
    /// Meta diária de trabalho em segundos (null = não alterar, 1800–86400)
    /// </summary>
    public int? WorkGoalSeconds { get; init; }
}
