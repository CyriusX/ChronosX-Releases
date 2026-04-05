namespace TimeTrack.Agent.Contracts.Updates;

/// <summary>
/// Response from update check endpoint
/// </summary>
public sealed class UpdateCheckResponse
{
    /// <summary>
    /// Whether an update is available
    /// </summary>
    public bool HasUpdate { get; init; }

    /// <summary>
    /// Current installed version
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// Latest available version
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// URL to download the installer
    /// </summary>
    public string? DownloadUrl { get; init; }

    /// <summary>
    /// SHA256 checksum of the installer for verification
    /// </summary>
    public string? ChecksumSha256 { get; init; }

    /// <summary>
    /// Size of the installer in bytes
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Release notes markdown
    /// </summary>
    public string? ReleaseNotes { get; init; }

    /// <summary>
    /// URL to full release notes
    /// </summary>
    public string? ReleaseNotesUrl { get; init; }
}
