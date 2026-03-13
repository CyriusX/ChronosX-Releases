using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Work hours configuration DTO
/// SRP: Apenas representa configuração de horário de trabalho
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
