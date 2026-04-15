using TimeTrack.Agent.Contracts.Updates;

namespace TimeTrack.Update.Services;

/// <summary>
/// Result of an update installation
/// </summary>
public sealed class UpdateResult
{
    public bool Success { get; init; }
    public string? Version { get; init; }
    public string? ErrorMessage { get; init; }
    public bool CanRollback { get; init; }
    public string? BackupPath { get; init; }
}

/// <summary>
/// Result of a rollback operation
/// </summary>
public sealed class RollbackResult
{
    public bool Success { get; init; }
    public string? Version { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Result of signature verification
/// </summary>
public sealed class SignatureVerificationResult
{
    public bool IsValid { get; init; }
    public string? Publisher { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Orchestrates the update installation process
/// </summary>
public interface IUpdateOrchestrator
{
    /// <summary>
    /// Install an update from the specified URL or pre-downloaded installer path
    /// </summary>
    /// <param name="downloadUrl">URL to download installer (ignored if installerPath is provided)</param>
    /// <param name="checksum">SHA256 checksum of the installer</param>
    /// <param name="targetVersion">Target version to install</param>
    /// <param name="verifySignature">Whether to verify Authenticode signature</param>
    /// <param name="progress">Progress reporter</param>
    /// <param name="installerPath">Optional pre-downloaded installer path (skips download)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<UpdateResult> InstallUpdateAsync(
        string downloadUrl,
        string checksum,
        string targetVersion,
        bool verifySignature,
        IProgress<UpdateProgress>? progress = null,
        string? installerPath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback to a previous version
    /// </summary>
    Task<RollbackResult> RollbackAsync(
        string backupPath,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Find the most recent backup directory
    /// </summary>
    string? FindMostRecentBackup();
}
