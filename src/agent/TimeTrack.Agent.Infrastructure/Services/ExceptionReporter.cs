using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Reports exceptions to local JSONL logs and to the maintenance panel (best-effort).
/// Must never throw to the caller.
/// </summary>
public sealed class ExceptionReporter : IExceptionReporter
{
    private readonly ExceptionFileSink _fileSink;
    private readonly MaintenanceEventSink _maintenanceSink;
    private readonly ILogger<ExceptionReporter> _logger;

    public ExceptionReporter(
        ExceptionFileSink fileSink,
        MaintenanceEventSink maintenanceSink,
        ILogger<ExceptionReporter> logger)
    {
        _fileSink = fileSink;
        _maintenanceSink = maintenanceSink;
        _logger = logger;
    }

    public async Task ReportAsync(ExceptionReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(report.Component) || string.IsNullOrWhiteSpace(report.Operation))
            {
                return;
            }

            if (string.Equals(report.ExceptionType, typeof(OperationCanceledException).FullName, StringComparison.Ordinal) ||
                string.Equals(report.ExceptionType, typeof(TaskCanceledException).FullName, StringComparison.Ordinal))
            {
                return;
            }

            await _fileSink.WriteAsync(report, cancellationToken).ConfigureAwait(false);
            await _maintenanceSink.EmitCriticalAsync(report, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to report exception");
        }
    }
}

