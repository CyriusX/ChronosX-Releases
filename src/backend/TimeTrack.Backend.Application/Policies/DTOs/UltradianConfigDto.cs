using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Ultradian rhythm configuration DTO
/// SRP: Apenas representa configuração do ciclo Ultradian
/// </summary>
public sealed class UltradianConfigDto
{
    /// <summary>
    /// Duration of focus blocks in minutes (10-180)
    /// </summary>
    [JsonPropertyName("focusMinutes")]
    public int FocusMinutes { get; init; } = 90;

    /// <summary>
    /// Duration of recovery breaks in minutes (5-60)
    /// </summary>
    [JsonPropertyName("breakMinutes")]
    public int BreakMinutes { get; init; } = 20;
}
