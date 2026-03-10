namespace TimeTrack.Agent.Application.UseCases.TrackingControl;

/// <summary>
/// Request para pausar o tracking
/// </summary>
public sealed record PauseTrackingRequest
{
    /// <summary>
    /// Motivo da pausa
    /// </summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Identificador de quem pausou (user_id ou "system")
    /// </summary>
    public required string PausedBy { get; init; }

    /// <summary>
    /// Se é pausa por política (idle, horário, etc)
    /// </summary>
    public bool IsPolicy { get; init; }
}

/// <summary>
/// Response da pausa do tracking
/// </summary>
public sealed record PauseTrackingResponse
{
    /// <summary>
    /// Status atual do tracking
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Momento em que foi pausado
    /// </summary>
    public DateTime PausedAt { get; init; }
}
