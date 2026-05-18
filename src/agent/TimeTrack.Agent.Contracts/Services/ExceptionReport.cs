namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Normalized exception payload for local logging and maintenance reporting.
/// Must not contain secrets (JWTs, refresh tokens, auth headers).
/// </summary>
public sealed record ExceptionReport
{
    public required DateTime OccurredAtUtc { get; init; }
    public required string Component { get; init; } // "AgentService" | "DesktopHost"
    public required string Operation { get; init; } // e.g. "ipc.command.StartTracking"

    public required string ExceptionType { get; init; }
    public required string Message { get; init; }
    public required string StackTrace { get; init; }

    public string? InnerExceptionType { get; init; }
    public string? InnerMessage { get; init; }
    public string? InnerStackTrace { get; init; }

    /// <summary>
    /// Safe metadata (strings only). Never include secrets.
    /// </summary>
    public Dictionary<string, string?>? Context { get; init; }

    public static bool IsCancellation(Exception ex)
        => ex is OperationCanceledException or TaskCanceledException;

    public static ExceptionReport FromException(
        Exception ex,
        string component,
        string operation,
        Dictionary<string, string?>? context = null,
        DateTime? occurredAtUtc = null)
    {
        var inner = ex.InnerException;
        return new ExceptionReport
        {
            OccurredAtUtc = occurredAtUtc ?? DateTime.UtcNow,
            Component = component,
            Operation = operation,
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            Message = ex.Message,
            StackTrace = ex.StackTrace ?? string.Empty,
            InnerExceptionType = inner?.GetType().FullName,
            InnerMessage = inner?.Message,
            InnerStackTrace = inner?.StackTrace,
            Context = context
        };
    }
}

