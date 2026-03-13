namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Configuration for focus mode policy (Pomodoro / Ultradian)
/// SRP: Apenas encapsula configuração de modo de foco
/// </summary>
public sealed class FocusModeConfig
{
    /// <summary>
    /// Whether focus mode is enabled for the organization
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Focus mode type: "pomodoro", "ultradian", or "none"
    /// </summary>
    public string Mode { get; set; } = "none";

    /// <summary>
    /// Whether users can manually start/stop focus cycles
    /// </summary>
    public bool AllowUserOverride { get; set; } = true;

    /// <summary>
    /// Pomodoro-specific configuration
    /// </summary>
    public PomodoroConfig? Pomodoro { get; set; }

    /// <summary>
    /// Ultradian-specific configuration
    /// </summary>
    public UltradianConfig? Ultradian { get; set; }
}
