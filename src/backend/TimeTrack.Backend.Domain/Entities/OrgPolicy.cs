using System.Text.Json;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa a política unificada de uma organização
/// Uma única política por organização contendo todas as configurações
///
/// SRP: Apenas gerencia a persistência e versionamento da política
/// Configurações específicas são delegadas para classes dedicadas:
/// - WorkHoursConfig: Configuração de horário de trabalho
/// - FocusModeConfig: Configuração de modo de foco
/// </summary>
public sealed class OrgPolicy
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public int Version { get; private set; } = 1;

    // Work Hours configuration stored as JSON
    public string WorkHoursJson { get; private set; } = "{}";

    // App exclusions stored as JSON array
    public string AppExclusionsJson { get; private set; } = "[]";

    // Focus Mode configuration stored as JSON
    public string FocusModeJson { get; private set; } = "{}";

    // Simple fields
    public int IdleThresholdSeconds { get; private set; } = 180;
    public int? IdleJustificationPromptThresholdSeconds { get; private set; }
    public int RetentionDays { get; private set; } = 90;

    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    private OrgPolicy() { }

    /// <summary>
    /// Creates a new organization policy with default values
    /// </summary>
    public static OrgPolicy Create(Guid orgId)
    {
        var defaultWorkHours = new WorkHoursConfig
        {
            Timezone = "America/Sao_Paulo",
            Days = new List<string> { "monday", "tuesday", "wednesday", "thursday", "friday" },
            StartTime = "08:00",
            EndTime = "18:00"
        };

        var defaultFocusMode = new FocusModeConfig
        {
            Enabled = false,
            Mode = "none",
            AllowUserOverride = true,
            Pomodoro = new PomodoroConfig
            {
                FocusMinutes = 25,
                ShortBreakMinutes = 5,
                LongBreakMinutes = 15,
                CyclesBeforeLongBreak = 4
            },
            Ultradian = new UltradianConfig
            {
                FocusMinutes = 90,
                BreakMinutes = 20
            }
        };

        return new OrgPolicy
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Version = 1,
            WorkHoursJson = JsonSerializer.Serialize(defaultWorkHours, JsonOptions),
            AppExclusionsJson = "[]",
            FocusModeJson = JsonSerializer.Serialize(defaultFocusMode, JsonOptions),
            IdleThresholdSeconds = 180,
            RetentionDays = 90,
            CreatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Updates the policy configuration
    /// </summary>
    public void Update(
        string? workHoursJson,
        string? appExclusionsJson,
        int? idleThresholdSeconds,
        int? retentionDays,
        string? focusModeJson = null)
    {
        Update(
            workHoursJson,
            appExclusionsJson,
            idleThresholdSeconds,
            idleJustificationPromptThresholdSpecified: false,
            idleJustificationPromptThresholdSeconds: null,
            retentionDays,
            focusModeJson);
    }

    public void Update(
        string? workHoursJson,
        string? appExclusionsJson,
        int? idleThresholdSeconds,
        bool idleJustificationPromptThresholdSpecified,
        int? idleJustificationPromptThresholdSeconds,
        int? retentionDays,
        string? focusModeJson = null)
    {
        if (workHoursJson != null)
            WorkHoursJson = workHoursJson;

        if (appExclusionsJson != null)
            AppExclusionsJson = appExclusionsJson;

        if (idleThresholdSeconds.HasValue)
            IdleThresholdSeconds = idleThresholdSeconds.Value;

        if (idleJustificationPromptThresholdSpecified)
            IdleJustificationPromptThresholdSeconds = idleJustificationPromptThresholdSeconds;

        if (retentionDays.HasValue)
            RetentionDays = retentionDays.Value;

        if (focusModeJson != null)
            FocusModeJson = focusModeJson;

        Version++;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the work hours configuration deserialized
    /// </summary>
    public WorkHoursConfig? GetWorkHours()
    {
        return JsonSerializer.Deserialize<WorkHoursConfig>(WorkHoursJson, JsonOptions);
    }

    /// <summary>
    /// Gets the app exclusions list deserialized
    /// </summary>
    public List<string>? GetAppExclusions()
    {
        return JsonSerializer.Deserialize<List<string>>(AppExclusionsJson, JsonOptions);
    }

    /// <summary>
    /// Gets the focus mode configuration deserialized
    /// </summary>
    public FocusModeConfig? GetFocusMode()
    {
        if (string.IsNullOrWhiteSpace(FocusModeJson) || FocusModeJson == "{}")
            return null;

        return JsonSerializer.Deserialize<FocusModeConfig>(FocusModeJson, JsonOptions);
    }
}
