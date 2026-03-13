using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Focus mode configuration DTO (Pomodoro / Ultradian)
/// SRP: Apenas representa configuração de modo de foco
/// </summary>
public sealed class FocusModeDto
{
    /// <summary>
    /// Whether focus mode is enabled for the organization
    /// </summary>
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    /// <summary>
    /// Focus mode type: "pomodoro", "ultradian", or "none"
    /// </summary>
    [JsonPropertyName("mode")]
    public string Mode { get; init; } = "none";

    /// <summary>
    /// Whether users can manually start/stop focus cycles
    /// </summary>
    [JsonPropertyName("allowUserOverride")]
    public bool AllowUserOverride { get; init; } = true;

    /// <summary>
    /// Pomodoro-specific configuration
    /// </summary>
    [JsonPropertyName("pomodoro")]
    public PomodoroConfigDto? Pomodoro { get; init; }

    /// <summary>
    /// Ultradian-specific configuration
    /// </summary>
    [JsonPropertyName("ultradian")]
    public UltradianConfigDto? Ultradian { get; init; }
}
