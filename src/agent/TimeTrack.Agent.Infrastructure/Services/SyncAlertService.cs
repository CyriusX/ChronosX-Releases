using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Serviço para registrar alertas no Windows Event Log
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SyncAlertService
{
    private readonly ISyncErrorRepository _syncErrorRepository;
    private readonly ILogger<SyncAlertService> _logger;
    private readonly int _consecutiveFailuresThreshold;

    private const string EventSource = "TimeTrack Agent";
    private const string EventLogName = "Application";

    public SyncAlertService(
        ISyncErrorRepository syncErrorRepository,
        ILogger<SyncAlertService> logger,
        int consecutiveFailuresThreshold = 5)
    {
        _syncErrorRepository = syncErrorRepository;
        _logger = logger;
        _consecutiveFailuresThreshold = consecutiveFailuresThreshold;

        EnsureEventSourceExists();
    }

    private void EnsureEventSourceExists()
    {
        try
        {
            if (!EventLog.SourceExists(EventSource))
            {
                EventLog.CreateEventSource(EventSource, EventLogName);
                _logger.LogInformation("Created Windows Event Log source: {Source}", EventSource);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Windows Event Log source");
        }
    }

    /// <summary>
    /// Verifica se deve alertar sobre falhas consecutivas e registra no Event Log
    /// </summary>
    public async Task CheckAndAlertConsecutiveFailuresAsync(CancellationToken cancellationToken = default)
    {
        var consecutiveFailures = await _syncErrorRepository.CountConsecutiveFailuresAsync(cancellationToken);

        if (consecutiveFailures >= _consecutiveFailuresThreshold)
        {
            var lastError = await _syncErrorRepository.GetLatestAsync(cancellationToken);

            var message = lastError is not null
                ? $"TimeTrack Agent: {consecutiveFailures} consecutive sync failures. Last error: {lastError.ErrorMessage} (Status: {lastError.StatusCode}, Endpoint: {lastError.Endpoint})"
                : $"TimeTrack Agent: {consecutiveFailures} consecutive sync failures detected.";

            try
            {
                EventLog.WriteEntry(
                    EventSource,
                    message,
                    EventLogEntryType.Error,
                    1001);

                _logger.LogWarning("Alerted {Count} consecutive failures to Windows Event Log", consecutiveFailures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to write to Windows Event Log");
            }
        }
    }
}
