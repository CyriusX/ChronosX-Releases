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
    public LocalSettings WithUpdates(
        bool? autoResumeNotification = null,
        bool? notificationSounds = null,
        string? language = null)
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
            UpdatedAt = this.UpdatedAt
        };
    }
}
