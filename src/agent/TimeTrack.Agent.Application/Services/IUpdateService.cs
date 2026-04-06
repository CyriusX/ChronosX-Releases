using TimeTrack.Agent.Contracts.Updates;

namespace TimeTrack.Agent.Application.Services;

/// <summary>
/// Service for managing application updates
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// Current update progress (null if no update in progress)
    /// </summary>
    UpdateProgress? CurrentProgress { get; }

    /// <summary>
    /// Whether an update is currently in progress
    /// </summary>
    bool IsUpdating { get; }

    /// <summary>
    /// Last checked update info (cached)
    /// </summary>
    UpdateCheckResponse? LastCheckResult { get; }

    /// <summary>
    /// Check for available updates
    /// </summary>
    Task<UpdateCheckResponse?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Start the update process (forced - no user confirmation needed)
    /// </summary>
    Task StartUpdateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when update progress changes
    /// </summary>
    event EventHandler<UpdateProgress>? ProgressChanged;

    /// <summary>
    /// Event raised when an update is available
    /// </summary>
    event EventHandler<UpdateCheckResponse>? UpdateAvailable;

    /// <summary>
    /// Event raised when update completes (success or failure)
    /// </summary>
    event EventHandler<UpdateResult>? UpdateCompleted;
}
