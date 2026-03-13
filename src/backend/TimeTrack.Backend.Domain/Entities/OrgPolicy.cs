using System.Text.Json;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa a política unificada de uma organização
/// Uma única política por organização contendo todas as configurações
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

    // Simple fields
    public int IdleThresholdSeconds { get; private set; } = 180;
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

        return new OrgPolicy
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Version = 1,
            WorkHoursJson = JsonSerializer.Serialize(defaultWorkHours, JsonOptions),
            AppExclusionsJson = "[]",
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
        int? retentionDays)
    {
        if (workHoursJson != null)
            WorkHoursJson = workHoursJson;

        if (appExclusionsJson != null)
            AppExclusionsJson = appExclusionsJson;

        if (idleThresholdSeconds.HasValue)
            IdleThresholdSeconds = idleThresholdSeconds.Value;

        if (retentionDays.HasValue)
            RetentionDays = retentionDays.Value;

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
}

/// <summary>
/// Configuration for work hours policy
/// </summary>
public sealed class WorkHoursConfig
{
    public string Timezone { get; set; } = "America/Sao_Paulo";
    public List<string> Days { get; set; } = new();
    public string StartTime { get; set; } = "08:00";
    public string EndTime { get; set; } = "18:00";

    /// <summary>
    /// Checks if a given UTC datetime falls within the work hours window
    /// </summary>
    public bool IsWithinWindow(DateTimeOffset utcTime)
    {
        try
        {
            var tz = TimeZoneInfo.FindSystemTimeZoneById(Timezone);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utcTime.UtcDateTime, tz);

            var dayName = local.DayOfWeek.ToString().ToLowerInvariant();
            if (!Days.Contains(dayName))
                return false;

            var localTime = TimeOnly.Parse($"{local.Hour:D2}:{local.Minute:D2}");
            var startTime = TimeOnly.Parse(StartTime);
            var endTime = TimeOnly.Parse(EndTime);

            return localTime >= startTime && localTime <= endTime;
        }
        catch
        {
            // If timezone is invalid, default to true (don't block)
            return true;
        }
    }
}
