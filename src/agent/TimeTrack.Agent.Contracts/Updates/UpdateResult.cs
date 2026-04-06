namespace TimeTrack.Agent.Contracts.Updates;

/// <summary>
/// Result of an update operation
/// </summary>
public sealed class UpdateResult
{
    /// <summary>
    /// Whether the update was successful
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The version that was installed
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Error message if the update failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether a restart is required to complete the update
    /// </summary>
    public bool RestartRequired { get; init; }
}
