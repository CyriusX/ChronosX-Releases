namespace TimeTrack.Backend.Domain.ValueObjects;

/// <summary>
/// Tipo de política
/// </summary>
public enum PolicyType
{
    /// <summary>
    /// Horário de trabalho
    /// </summary>
    WorkHours = 1,

    /// <summary>
    /// Exclusões de apps
    /// </summary>
    AppExclusions = 2,

    /// <summary>
    /// Modo de foco (Pomodoro/Ultradian)
    /// </summary>
    FocusMode = 3
}
