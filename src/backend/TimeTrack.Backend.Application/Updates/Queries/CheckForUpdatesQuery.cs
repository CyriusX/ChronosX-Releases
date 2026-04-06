using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Updates.DTOs;

namespace TimeTrack.Backend.Application.Updates.Queries;

/// <summary>
/// Query to check for application updates
/// </summary>
public sealed record CheckForUpdatesQuery(
    string CurrentVersion,
    string Channel = "stable") : IRequest<UpdateCheckResponse?>;

/// <summary>
/// Handler for update check queries
/// </summary>
public sealed class CheckForUpdatesQueryHandler
    : IRequestHandler<CheckForUpdatesQuery, UpdateCheckResponse?>
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<CheckForUpdatesQueryHandler> _logger;

    public CheckForUpdatesQueryHandler(
        IConfiguration configuration,
        ILogger<CheckForUpdatesQueryHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<UpdateCheckResponse?> Handle(
        CheckForUpdatesQuery request,
        CancellationToken cancellationToken)
    {
        var updateConfig = _configuration.GetSection("Updates");
        var latestVersion = updateConfig.GetValue<string>("LatestVersion");
        var downloadUrl = updateConfig.GetValue<string>("DownloadUrl");
        var checksumSha256 = updateConfig.GetValue<string>("ChecksumSha256");
        var fileSizeBytes = updateConfig.GetValue<long?>("FileSizeBytes");
        var releaseNotes = updateConfig.GetValue<string>("ReleaseNotes");
        var minimumVersion = updateConfig.GetValue<string>("MinimumVersion");
        var isMandatory = updateConfig.GetValue<bool>("IsMandatory");
        var supportedChannels = updateConfig.GetSection("Channels").Get<string[]>() ?? ["stable"];

        // Validate channel
        if (!supportedChannels.Contains(request.Channel, StringComparer.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Unsupported update channel requested: {Channel}. Supported: {Supported}",
                request.Channel,
                string.Join(", ", supportedChannels));
            return Task.FromResult<UpdateCheckResponse?>(null);
        }

        // If no version configured, no update available
        if (string.IsNullOrEmpty(latestVersion))
        {
            _logger.LogDebug("No latest version configured");
            return Task.FromResult<UpdateCheckResponse?>(null);
        }

        // Compare versions
        var hasUpdate = IsNewerVersion(request.CurrentVersion, latestVersion);

        if (!hasUpdate)
        {
            _logger.LogDebug(
                "No update available. Current: {CurrentVersion}, Latest: {LatestVersion}",
                request.CurrentVersion,
                latestVersion);
            return Task.FromResult<UpdateCheckResponse?>(null);
        }

        _logger.LogInformation(
            "Update available: {LatestVersion} for channel {Channel}",
            latestVersion,
            request.Channel);

        var response = new UpdateCheckResponse
        {
            HasUpdate = true,
            CurrentVersion = request.CurrentVersion,
            LatestVersion = latestVersion,
            DownloadUrl = downloadUrl,
            ChecksumSha256 = checksumSha256,
            FileSizeBytes = fileSizeBytes,
            ReleaseNotes = releaseNotes,
            MinimumVersion = minimumVersion,
            IsMandatory = isMandatory || !string.IsNullOrEmpty(minimumVersion)
        };

        return Task.FromResult<UpdateCheckResponse?>(response);
    }

    /// <summary>
    /// Compare two version strings to determine if the new version is greater
    /// </summary>
    private static bool IsNewerVersion(string currentVersion, string latestVersion)
    {
        if (string.IsNullOrEmpty(currentVersion))
            return true;

        if (string.IsNullOrEmpty(latestVersion))
            return false;

        try
        {
            var current = new Version(currentVersion.TrimStart('v', 'V'));
            var latest = new Version(latestVersion.TrimStart('v', 'V'));
            return latest > current;
        }
        catch (FormatException)
        {
            // If versions can't be parsed, do string comparison
            return string.Compare(latestVersion, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;
        }
    }
}
