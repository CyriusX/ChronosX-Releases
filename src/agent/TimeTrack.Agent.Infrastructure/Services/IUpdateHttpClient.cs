using TimeTrack.Agent.Contracts.Updates;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// HTTP client for checking and downloading updates
/// </summary>
public interface IUpdateHttpClient
{
    /// <summary>
    /// Check if an update is available
    /// </summary>
    Task<UpdateCheckResponse?> CheckForUpdatesAsync(
        string currentVersion,
        string channel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Download an update installer with progress reporting
    /// </summary>
    Task<string> DownloadUpdateAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<UpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
