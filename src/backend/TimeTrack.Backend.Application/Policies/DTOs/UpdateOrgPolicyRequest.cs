using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Request DTO for updating organization policy
/// SRP: Apenas representa a requisição de atualização de política
/// </summary>
public sealed class UpdateOrgPolicyRequest
{
    private int? _idleJustificationPromptThresholdSeconds;

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
    /// Optional prompt threshold for idle justification in seconds. Null disables the feature.
    /// </summary>
    [JsonPropertyName("idleJustificationPromptThresholdSeconds")]
    public int? IdleJustificationPromptThresholdSeconds
    {
        get => _idleJustificationPromptThresholdSeconds;
        init
        {
            _idleJustificationPromptThresholdSeconds = value;
            IdleJustificationPromptThresholdSecondsSpecified = true;
        }
    }

    [JsonIgnore]
    public bool IdleJustificationPromptThresholdSecondsSpecified { get; private init; }

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
