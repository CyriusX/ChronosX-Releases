using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Response DTO for organization policy
/// SRP: Apenas representa a resposta completa de uma política
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
    /// Optional prompt threshold for idle justification in seconds. Null disables the feature.
    /// </summary>
    public int? IdleJustificationPromptThresholdSeconds { get; init; }

    /// <summary>
    /// Data retention period in days (30, 60, 90, 180, 365)
    /// </summary>
    public int RetentionDays { get; init; }

    /// <summary>
    /// Focus mode configuration (Pomodoro / Ultradian)
    /// </summary>
    public FocusModeDto FocusMode { get; init; } = new();

    /// <summary>
    /// When the policy was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the policy was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; init; }
}
