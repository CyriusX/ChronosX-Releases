namespace TimeTrack.Backend.Application.Policies.DTOs;

public sealed class EvidencePolicyResponse
{
    public bool ScreenshotsEnabled { get; init; }
    public int ScreenshotIntervalMinutes { get; init; }
    public List<string> ScreenshotExcludedApps { get; init; } = [];
    public int EvidenceRetentionDays { get; init; }
    public bool WebsiteTrackingEnabled { get; init; }
    public int Version { get; init; }
}
