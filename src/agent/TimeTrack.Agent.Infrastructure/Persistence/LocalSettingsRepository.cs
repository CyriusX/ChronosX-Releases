using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Implementação SQLite do repositório de configurações locais
///
/// SOLID:
/// - SRP: Apenas persistência de LocalSettings
/// - DIP: Implementa ILocalSettingsRepository
/// - OCP: Extensível para novas configurações
/// </summary>
public sealed class LocalSettingsRepository : ILocalSettingsRepository
{
    private readonly SqliteContext _context;
    private readonly ILogger<LocalSettingsRepository> _logger;

    public LocalSettingsRepository(
        SqliteContext context,
        ILogger<LocalSettingsRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LocalSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT
                id as Id,
                auto_resume_notification_enabled as AutoResumeNotificationEnabled,
                notification_sounds_enabled as NotificationSoundsEnabled,
                language as Language,
                idle_threshold_seconds as IdleThresholdSeconds,
                work_goal_seconds as WorkGoalSeconds,
                devtools_enabled as DevToolsEnabled,
                devtools_enabled_until_utc as DevToolsEnabledUntilUtc,
                updated_at as UpdatedAt
            FROM local_settings
            LIMIT 1";

        var dto = await connection.QueryFirstOrDefaultAsync<LocalSettingsDto>(sql);

        if (dto == null)
        {
            _logger.LogDebug("No local settings found, returning defaults");
            return LocalSettings.CreateDefault();
        }

        _logger.LogDebug("Local settings loaded: Language={Language}, IdleThreshold={IdleThreshold}",
            dto.Language, dto.IdleThresholdSeconds);

        DateTime? devToolsUntilUtc = null;
        if (!string.IsNullOrWhiteSpace(dto.DevToolsEnabledUntilUtc)
            && DateTime.TryParse(dto.DevToolsEnabledUntilUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
        {
            devToolsUntilUtc = parsed;
        }

        return Domain.Entities.LocalSettings.CreateDefault()
            .WithUpdates(
                autoResumeNotification: dto.AutoResumeNotificationEnabled == 1,
                notificationSounds: dto.NotificationSoundsEnabled == 1,
                language: dto.Language,
                idleThresholdSeconds: dto.IdleThresholdSeconds,
                workGoalSeconds: dto.WorkGoalSeconds)
            .WithDevToolsAccess(dto.DevToolsEnabled == 1, devToolsUntilUtc);
    }

    public async Task SaveAsync(LocalSettings settings, CancellationToken cancellationToken = default)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        // SQLite UPSERT usando INSERT OR REPLACE
        const string sql = @"
            INSERT OR REPLACE INTO local_settings
                (id, auto_resume_notification_enabled, notification_sounds_enabled, language, idle_threshold_seconds, work_goal_seconds, devtools_enabled, devtools_enabled_until_utc, updated_at)
            VALUES
                (@Id, @AutoResumeNotificationEnabled, @NotificationSoundsEnabled, @Language, @IdleThresholdSeconds, @WorkGoalSeconds, @DevToolsEnabled, @DevToolsEnabledUntilUtc, @UpdatedAt)";

        await connection.ExecuteAsync(sql, new
        {
            Id = "singleton", // Chave fixa para singleton
            AutoResumeNotificationEnabled = settings.AutoResumeNotificationEnabled ? 1 : 0,
            NotificationSoundsEnabled = settings.NotificationSoundsEnabled ? 1 : 0,
            Language = settings.Language,
            IdleThresholdSeconds = settings.IdleThresholdSeconds,
            WorkGoalSeconds = settings.WorkGoalSeconds,
            DevToolsEnabled = settings.DevToolsEnabled ? 1 : 0,
            DevToolsEnabledUntilUtc = settings.DevToolsEnabledUntilUtc?.ToString("O"),
            UpdatedAt = settings.UpdatedAt.ToString("O")
        });

        _logger.LogDebug("Local settings saved: AutoResume={AutoResume}, Sounds={Sounds}, Lang={Lang}",
            settings.AutoResumeNotificationEnabled,
            settings.NotificationSoundsEnabled,
            settings.Language);
    }

    /// <summary>
    /// DTO interno para mapeamento Dapper (nomes snake_case para SQLite)
    /// </summary>
    private sealed class LocalSettingsDto
    {
        public string Id { get; set; } = string.Empty;
        public int AutoResumeNotificationEnabled { get; set; }
        public int NotificationSoundsEnabled { get; set; }
        public string Language { get; set; } = "pt-BR";
        public int? IdleThresholdSeconds { get; set; }
        public int? WorkGoalSeconds { get; set; }
        public int DevToolsEnabled { get; set; }
        public string? DevToolsEnabledUntilUtc { get; set; }
        public string? UpdatedAt { get; set; }
    }
}
