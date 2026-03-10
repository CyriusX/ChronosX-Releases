namespace TimeTrack.Agent.Application.UseCases.TrackingControl;

/// <summary>
/// Request para retomar o tracking
/// </summary>
public sealed record ResumeTrackingRequest
{
    /// <summary>
    /// Identificador de quem retomou (user_id ou "system")
    /// </summary>
    public required string ResumedBy { get; init; }
}

/// <summary>
/// Response da retomada do tracking
/// </summary>
public sealed record ResumeTrackingResponse
{
    /// <summary>
    /// Status atual do tracking
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Momento em que foi retomado
    /// </summary>
    public DateTime ResumedAt { get; init; }
}
