namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Pomodoro technique configuration
/// SRP: Apenas encapsula configuração do Pomodoro
/// </summary>
public sealed class PomodoroConfig
{
    /// <summary>
    /// Duration of focus blocks in minutes (10-180)
    /// </summary>
    public int FocusMinutes { get; set; } = 25;

    /// <summary>
    /// Duration of short breaks in minutes (5-60)
    /// </summary>
    public int ShortBreakMinutes { get; set; } = 5;

    /// <summary>
    /// Duration of long breaks in minutes (5-60)
    /// </summary>
    public int LongBreakMinutes { get; set; } = 15;

    /// <summary>
    /// Number of cycles before a long break (2-8)
    /// </summary>
    public int CyclesBeforeLongBreak { get; set; } = 4;
}
