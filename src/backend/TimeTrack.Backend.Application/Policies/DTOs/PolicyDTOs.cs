using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Response DTO for organization policy
/// </summary>
public sealed class OrgPolicyResponse
{
    /// <summary>
    /// Unique identifier of the policy
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Organization ID
    /// </summary>
    public Guid OrgId { get; init; }

    /// <summary>
    /// Version number, incremented on each update
    /// </summary>
    public int Version { get; init; }

    /// <summary>
    /// Work hours configuration
    /// </summary>
    public WorkHoursDto WorkHours { get; init; } = new();

    /// <summary>
    /// List of excluded app executable hashes
    /// </summary>
    public List<string> AppExclusions { get; init; } = new();

    /// <summary>
    /// Idle threshold in seconds (60-3600)
    /// </summary>
    public int IdleThresholdSeconds { get; init; }

    /// <summary>
    /// Data retention period in days (30, 60, 90, 180, 365)
    /// </summary>
    public int RetentionDays { get; init; }

    /// <summary>
    /// When the policy was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the policy was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}

/// <summary>
/// Request DTO for updating organization policy
/// </summary>
public sealed class UpdateOrgPolicyRequest
{
    /// <summary>
    /// Work hours configuration
    /// </summary>
    [JsonPropertyName("workHours")]
    public WorkHoursDto? WorkHours { get; init; }

    /// <summary>
    /// List of excluded app executable hashes
    /// </summary>
    [JsonPropertyName("appExclusions")]
    public List<string>? AppExclusions { get; init; }

    /// <summary>
    /// Idle threshold in seconds (60-3600)
    /// </summary>
    [JsonPropertyName("idleThresholdSeconds")]
    public int? IdleThresholdSeconds { get; init; }

    /// <summary>
    /// Data retention period in days (30, 60, 90, 180, 365)
    /// </summary>
    [JsonPropertyName("retentionDays")]
    public int? RetentionDays { get; init; }
}

/// <summary>
/// Work hours configuration DTO
/// </summary>
public sealed class WorkHoursDto
{
    /// <summary>
    /// IANA timezone identifier (e.g., "America/Sao_Paulo")
    /// </summary>
    [JsonPropertyName("timezone")]
    public string Timezone { get; init; } = "America/Sao_Paulo";

    /// <summary>
    /// Days of the week when work hours apply
    /// Values: "monday", "tuesday", "wednesday", "thursday", "friday", "saturday", "sunday"
    /// </summary>
    [JsonPropertyName("days")]
    public List<string> Days { get; init; } = new() { "monday", "tuesday", "wednesday", "thursday", "friday" };

    /// <summary>
    /// Start time in HH:mm format (24-hour)
    /// </summary>
    [JsonPropertyName("startTime")]
    public string StartTime { get; init; } = "08:00";

    /// <summary>
    /// End time in HH:mm format (24-hour)
    /// </summary>
    [JsonPropertyName("endTime")]
    public string EndTime { get; init; } = "18:00";
}
