namespace TimeTrack.Update.Services;

/// <summary>
/// Manages backup and restore of installation files
/// </summary>
public interface IBackupManager
{
    /// <summary>
    /// Create a backup of the current installation
    /// </summary>
    /// <returns>Path to the backup directory</returns>
    Task<string> CreateBackupAsync(string installPath, string version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restore installation from a backup
    /// </summary>
    Task<bool> RestoreBackupAsync(string backupPath, string installPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clean up old backups beyond retention period
    /// </summary>
    Task CleanupOldBackupsAsync(int retentionDays, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get list of available backups
    /// </summary>
    IEnumerable<BackupInfo> GetAvailableBackups();

    /// <summary>
    /// Get the backup directory path
    /// </summary>
    string BackupDirectory { get; }
}

/// <summary>
/// Information about a backup
/// </summary>
public sealed class BackupInfo
{
    public string Path { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public long SizeBytes { get; init; }
}
