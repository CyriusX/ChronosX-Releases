using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Policies.DTOs;

/// <summary>
/// Pomodoro technique configuration DTO
/// SRP: Apenas representa configuração do Pomodoro
/// </summary>
public sealed class PomodoroConfigDto
{
    /// <summary>
    /// Duration of focus blocks in minutes (10-180)
    /// </summary>
    [JsonPropertyName("focusMinutes")]
    public int FocusMinutes { get; init; } = 25;

    /// <summary>
    /// Duration of short breaks in minutes (5-60)
    /// </summary>
    [JsonPropertyName("shortBreakMinutes")]
    public int ShortBreakMinutes { get; init; } = 5;

    /// <summary>
    /// Duration of long breaks in minutes (5-60)
    /// </summary>
    [JsonPropertyName("longBreakMinutes")]
    public int LongBreakMinutes { get; init; } = 15;

    /// <summary>
    /// Number of cycles before a long break (2-8)
    /// </summary>
    [JsonPropertyName("cyclesBeforeLongBreak")]
    public int CyclesBeforeLongBreak { get; init; } = 4;
}
