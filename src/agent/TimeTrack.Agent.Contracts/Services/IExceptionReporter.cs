namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Reports exceptions to local machine logs and to the maintenance panel (best-effort).
/// Implementations must never throw to the caller.
/// </summary>
public interface IExceptionReporter
{
    Task ReportAsync(ExceptionReport report, CancellationToken cancellationToken = default);
}

