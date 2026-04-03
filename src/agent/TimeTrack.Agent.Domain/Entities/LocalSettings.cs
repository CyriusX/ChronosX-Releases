namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Configurações locais do colaborador (preferências pessoais)
/// Persistidas em SQLite no dispositivo local
///
/// SOLID:
/// - SRP: Gerencia apenas preferências locais do usuário
/// - OCP: Extensível via novas propriedades sem modificar comportamento existente
/// </summary>
public sealed class LocalSettings
{
    /// <summary>
    /// Identificador único (sempre Guid.Empty para singleton)
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Toggle para notificação de auto-resume quando tracking pausado por muito tempo
    /// </summary>
    public bool AutoResumeNotificationEnabled { get; private set; } = true;

    /// <summary>
    /// Toggle para sons de notificação
    /// </summary>
    public bool NotificationSoundsEnabled { get; private set; } = true;

    /// <summary>
    /// Idioma da interface (pt-BR, en-US)
    /// </summary>
    public string Language { get; private set; } = "pt-BR";

    /// <summary>
    /// Limiar de inatividade em segundos (60–3600). Null = usar padrão do Agent (300s).
    /// </summary>
    public int? IdleThresholdSeconds { get; private set; }

    /// <summary>
    /// Meta diária de trabalho em segundos (1800–86400). Null = sem meta (padrão 28800 = 8h).
    /// </summary>
    public int? WorkGoalSeconds { get; private set; }

    /// <summary>
    /// Timestamp da última atualização
    /// </summary>
    public DateTime UpdatedAt { get; private set; }

    private LocalSettings() { }

    /// <summary>
    /// Cria configurações com valores padrão
    /// </summary>
    public static LocalSettings CreateDefault()
    {
        return new LocalSettings
        {
            Id = Guid.Empty, // Singleton - sempre mesmo ID
            AutoResumeNotificationEnabled = true,
            NotificationSoundsEnabled = true,
            Language = "pt-BR",
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Atualiza toggle de notificação de auto-resume
    /// </summary>
    public LocalSettings WithAutoResumeNotification(bool enabled)
    {
        var settings = Clone();
        settings.AutoResumeNotificationEnabled = enabled;
        settings.UpdatedAt = DateTime.UtcNow;
        return settings;
    }

    /// <summary>
    /// Atualiza toggle de sons de notificação
    /// </summary>
    public LocalSettings WithNotificationSounds(bool enabled)
    {
        var settings = Clone();
        settings.NotificationSoundsEnabled = enabled;
        settings.UpdatedAt = DateTime.UtcNow;
        return settings;
    }

    /// <summary>
    /// Atualiza idioma da interface
    /// </summary>
    public LocalSettings WithLanguage(string language)
    {
        if (language != "pt-BR" && language != "en-US")
            throw new ArgumentException("Language must be 'pt-BR' or 'en-US'", nameof(language));

        var settings = Clone();
        settings.Language = language;
        settings.UpdatedAt = DateTime.UtcNow;
        return settings;
    }

    /// <summary>
    /// Atualiza múltiplas configurações de uma vez
    /// </summary>
    public LocalSettings WithIdleThreshold(int? seconds)
    {
        if (seconds.HasValue && (seconds.Value < 60 || seconds.Value > 3600))
            throw new ArgumentOutOfRangeException(nameof(seconds), "Idle threshold must be between 60 and 3600 seconds");

        var settings = Clone();
        settings.IdleThresholdSeconds = seconds;
        settings.UpdatedAt = DateTime.UtcNow;
        return settings;
    }

    public LocalSettings WithUpdates(
        bool? autoResumeNotification = null,
        bool? notificationSounds = null,
        string? language = null,
        int? idleThresholdSeconds = null,
        int? workGoalSeconds = null)
    {
        var settings = Clone();

        if (autoResumeNotification.HasValue)
            settings.AutoResumeNotificationEnabled = autoResumeNotification.Value;

        if (notificationSounds.HasValue)
            settings.NotificationSoundsEnabled = notificationSounds.Value;

        if (!string.IsNullOrEmpty(language))
        {
            if (language != "pt-BR" && language != "en-US")
                throw new ArgumentException("Language must be 'pt-BR' or 'en-US'", nameof(language));
            settings.Language = language;
        }

        if (idleThresholdSeconds.HasValue)
        {
            if (idleThresholdSeconds.Value < 60 || idleThresholdSeconds.Value > 3600)
                throw new ArgumentOutOfRangeException(nameof(idleThresholdSeconds), "Idle threshold must be between 60 and 3600 seconds");
            settings.IdleThresholdSeconds = idleThresholdSeconds.Value;
        }

        if (workGoalSeconds.HasValue)
        {
            if (workGoalSeconds.Value < 1800 || workGoalSeconds.Value > 86400)
                throw new ArgumentOutOfRangeException(nameof(workGoalSeconds), "Work goal must be between 1800 and 86400 seconds (30min–24h)");
            settings.WorkGoalSeconds = workGoalSeconds.Value;
        }

        settings.UpdatedAt = DateTime.UtcNow;
        return settings;
    }

    private LocalSettings Clone()
    {
        return new LocalSettings
        {
            Id = this.Id,
            AutoResumeNotificationEnabled = this.AutoResumeNotificationEnabled,
            NotificationSoundsEnabled = this.NotificationSoundsEnabled,
            Language = this.Language,
            IdleThresholdSeconds = this.IdleThresholdSeconds,
            WorkGoalSeconds = this.WorkGoalSeconds,
            UpdatedAt = this.UpdatedAt
        };
    }
}
