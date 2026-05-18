namespace TimeTrack.Backend.AI.Configuration;

public sealed class ZAiOptions
{
    public const string SectionName = "ZAi";

    public string ApiKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = "glm-4.5-flash";
    public string ModelVersion { get; set; } = "glm-4.5-flash";
    public int TimeoutSeconds { get; set; } = 10;
    public int MaxRetries { get; set; } = 3;
    public int CircuitBreakerFailures { get; set; } = 5;
    public int CircuitBreakerDurationSeconds { get; set; } = 60;
    public int CacheDurationHours { get; set; } = 24;
    public int HighConfidenceCacheHours { get; set; } = 168;

    /// <summary>
    /// Maps decision types to specific AI models. Falls back to <see cref="Model"/> if not set.
    /// </summary>
    public Dictionary<string, string> TaskModelMap { get; set; } = new()
    {
        ["app_classification"] = "glm-4.5-flash",
        ["alert_generated"] = "glm-4.5-flash",
        ["pattern_detected"] = "glm-4.5-flash",
        ["weekly_narrative"] = "glm-4.5-flash",
        ["reports_suggestion"] = "glm-4.5-flash",
        ["weekly_email_report"] = "glm-4.5-flash",
    };

    public string ResolveModel(string decisionType)
    {
        return TaskModelMap.TryGetValue(decisionType, out var model) ? model : Model;
    }
}
