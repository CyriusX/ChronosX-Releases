namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Ultradian rhythm configuration
/// SRP: Apenas encapsula configuração do ciclo Ultradian
/// </summary>
public sealed class UltradianConfig
{
    /// <summary>
    /// Duration of focus blocks in minutes (10-180)
    /// </summary>
    public int FocusMinutes { get; set; } = 90;

    /// <summary>
    /// Duration of recovery breaks in minutes (5-60)
    /// </summary>
    public int BreakMinutes { get; set; } = 20;
}
