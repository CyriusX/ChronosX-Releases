using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Request DTO for updating organization policy
/// SRP: Apenas representa a requisição de atualização de política
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

    /// <summary>
    /// Focus mode configuration (Pomodoro / Ultradian)
    /// </summary>
    [JsonPropertyName("focusMode")]
    public FocusModeDto? FocusMode { get; init; }
}
