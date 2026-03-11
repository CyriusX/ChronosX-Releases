namespace TimeTrack.Agent.Application.UseCases.TrackingControl;

/// <summary>
/// Request para iniciar o tracking
/// </summary>
public sealed record StartTrackingRequest
{
    /// <summary>
    /// Identificador de quem iniciou (user_id ou "system")
    /// </summary>
    public required string StartedBy { get; init; }
}

/// <summary>
/// Response do início do tracking
/// </summary>
public sealed record StartTrackingResponse
{
    /// <summary>
    /// Status atual do tracking
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Momento em que foi iniciado
    /// </summary>
    public DateTime StartedAt { get; init; }

    /// <summary>
    /// Se foi criado novo estado ou reativado
    /// </summary>
    public bool WasCreated { get; init; }
}

/// <summary>
/// Request para parar o tracking
/// </summary>
public sealed record StopTrackingRequest
{
    /// <summary>
    /// Identificador de quem parou (user_id ou "system")
    /// </summary>
    public required string StoppedBy { get; init; }

    /// <summary>
    /// Motivo opcional para parar
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Response de parar o tracking
/// </summary>
public sealed record StopTrackingResponse
{
    /// <summary>
    /// Status atual do tracking
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// Momento em que foi parado
    /// </summary>
    public DateTime StoppedAt { get; init; }
}
