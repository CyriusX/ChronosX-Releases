namespace TimeTrack.Agent.Contracts.Updates;

/// <summary>
/// Progress report for download operations
/// </summary>
public sealed class UpdateDownloadProgress
{
    /// <summary>
    /// Bytes downloaded so far
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download
    /// </summary>
    public long BytesTotal { get; init; }

    /// <summary>
    /// Download speed in bytes per second
    /// </summary>
    public long BytesPerSecond { get; init; }

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int Percentage => BytesTotal > 0 ? (int)((BytesDownloaded * 100) / BytesTotal) : 0;
}
