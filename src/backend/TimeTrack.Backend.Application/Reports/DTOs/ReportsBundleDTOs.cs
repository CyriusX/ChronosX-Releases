using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TimeTrack.Backend.Application.Reports.DTOs;

public sealed class ReportsBundleError
{
    [JsonPropertyName("section")]
    public string Section { get; init; } = string.Empty;

    [JsonPropertyName("code")]
    public string Code { get; init; } = "internal_error";
}

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

    /// <summary>
    /// Non-fatal errors: if present, the bundle returned partial data with the failed sections listed here.
    /// </summary>
    [JsonPropertyName("errors")]
    public List<ReportsBundleError> Errors { get; init; } = [];
}
