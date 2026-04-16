using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Reports.DTOs;

/// <summary>
/// Composite response for the reports page to avoid client-side fan-out and rate limiting.
/// </summary>
public sealed class ReportsBundleResponse
{
    [JsonPropertyName("dailySummaryRange")]
    public DailySummaryRangeResponse DailySummaryRange { get; init; } = new();

    [JsonPropertyName("productivityTrend")]
    public ProductivityTrendResponse ProductivityTrend { get; init; } = new();

    [JsonPropertyName("topApps")]
    public TopAppsResponse TopApps { get; init; } = new();

    [JsonPropertyName("topPaths")]
    public TopPathsResponse TopPaths { get; init; } = new();

    [JsonPropertyName("distractionStats")]
    public DistractionStatsResponse DistractionStats { get; init; } = new();

    [JsonPropertyName("categoryDistribution")]
    public CategoryDistributionResponse CategoryDistribution { get; init; } = new();

    /// <summary>
    /// Top accessed folders (file system).
    /// This is populated once the agent starts sending FilePath consistently.
    /// </summary>
    [JsonPropertyName("topFolders")]
    public TopFoldersResponse? TopFolders { get; init; }
}

