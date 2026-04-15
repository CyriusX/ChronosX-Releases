namespace TimeTrack.Agent.Contracts.Updates;

/// <summary>
/// Represents the current state of an update operation
/// </summary>
public sealed class UpdateProgress
{
    /// <summary>
    /// Current stage of the update process
    /// </summary>
    public UpdateStage Stage { get; init; } = UpdateStage.Idle;

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Human-readable status message
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Download speed in bytes per second (when downloading)
    /// </summary>
    public long? BytesPerSecond { get; init; }

    /// <summary>
    /// Bytes downloaded so far
    /// </summary>
    public long? BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download
    /// </summary>
    public long? BytesTotal { get; init; }

    /// <summary>
    /// Target version being installed
    /// </summary>
    public string? TargetVersion { get; init; }

    /// <summary>
    /// Error message if stage is Failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether rollback is available after failure
    /// </summary>
    public bool CanRollback { get; init; }
}

/// <summary>
/// Stages of the update process
/// </summary>
public enum UpdateStage
{
    /// <summary>
    /// No update in progress
    /// </summary>
    Idle,

    /// <summary>
    /// Checking for updates
    /// </summary>
    Checking,

    /// <summary>
    /// Update available, waiting to start
    /// </summary>
    Available,

    /// <summary>
    /// Downloading installer
    /// </summary>
    Downloading,

    /// <summary>
    /// Verifying checksum/signature
    /// </summary>
    Verifying,

    /// <summary>
    /// Creating backup of current version
    /// </summary>
    BackingUp,

    /// <summary>
    /// Stopping services
    /// </summary>
    StoppingServices,

    /// <summary>
    /// Installing new version
    /// </summary>
    Installing,

    /// <summary>
    /// Starting services
    /// </summary>
    StartingServices,

    /// <summary>
    /// Update completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Update failed
    /// </summary>
    Failed,

    /// <summary>
    /// Rolling back to previous version
    /// </summary>
    RollingBack
}
