namespace TimeTrack.Backend.Application.Updates.DTOs;

/// <summary>
/// Response for update check request
/// </summary>
public sealed class UpdateCheckResponse
{
    /// <summary>
    /// Whether an update is available
    /// </summary>
    public bool HasUpdate { get; init; }

    /// <summary>
    /// The latest available version
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// The current version on the server (for comparison)
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// URL to download the installer
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// SHA256 checksum of the installer file
    /// </summary>
    public string? ChecksumSha256 { get; init; }

    /// <summary>
    /// Size of the installer in bytes
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Release notes for the new version
    /// </summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// Minimum required version (if this is a forced update)
    /// </summary>
    public string? MinimumVersion { get; init; }

    /// <summary>
    /// Whether this update is mandatory
    /// </summary>
    public bool IsMandatory { get; init; }
}
