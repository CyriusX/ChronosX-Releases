using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Emits exception reports as agent events so they are synced to the backend
/// and displayed on the Maintenance page.
/// Must never throw to the caller.
/// </summary>
public sealed class MaintenanceEventSink
{
    private const string EventType = "exception.caught";

    private readonly IAgentEventLogger _eventLogger;
    private readonly ILogger<MaintenanceEventSink> _logger;

    public MaintenanceEventSink(IAgentEventLogger eventLogger, ILogger<MaintenanceEventSink> logger)
    {
        _eventLogger = eventLogger;
        _logger = logger;
    }

    public async Task EmitCriticalAsync(ExceptionReport report, CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = $"{report.Component}::{report.Operation} — {report.ExceptionType}: {report.Message}";
            await _eventLogger.LogAsync(
                EventType,
                AgentEventCategory.Error,
                AgentEventSeverity.Critical,
                summary,
                metadata: report,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Fire-and-forget
            _logger.LogDebug(ex, "Failed to emit maintenance exception event");
        }
    }
}

