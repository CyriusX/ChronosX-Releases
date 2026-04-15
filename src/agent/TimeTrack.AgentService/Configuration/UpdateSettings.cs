namespace TimeTrack.AgentService.Configuration;

/// <summary>
/// Configuration settings for the auto-update system
/// </summary>
public sealed class UpdateSettings
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "Update";

    /// <summary>
    /// Whether auto-update is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to verify Authenticode signatures before installing
    /// </summary>
    public bool VerifySignature { get; set; } = true;

    /// <summary>
    /// Update channel (stable, beta, canary)
    /// </summary>
    public string Channel { get; set; } = "stable";

    /// <summary>
    /// How often to check for updates (in minutes)
    /// </summary>
    public int CheckIntervalMinutes { get; set; } = 240;

    /// <summary>
    /// Base URL for update API
    /// </summary>
    public string UpdateUrl { get; set; } = "https://chronosx-timetrack-api.gpoda0.easypanel.host/api/v1/updates";

    /// <summary>
    /// Timeout for downloading updates (in minutes)
    /// </summary>
    public int DownloadTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// How many days to keep backup files
    /// </summary>
    public int BackupRetentionDays { get; set; } = 7;

    /// <summary>
    /// Check for updates on startup
    /// </summary>
    public bool CheckOnStartup { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed downloads
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts (in seconds)
    /// </summary>
    public int RetryDelaySeconds { get; set; } = 30;
}
