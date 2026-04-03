using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Remove métricas de máquina com mais de 24 horas.
/// Métricas são para monitoramento em tempo real, não análise histórica.
/// </summary>
public sealed class MachineMetricsCleanupJob : IMachineMetricsCleanupJob
{
    private readonly IMachineMetricsRepository _metricsRepository;
    private readonly IAgentEventLogRepository _eventLogRepository;
    private readonly ILogger<MachineMetricsCleanupJob> _logger;

    private const int MetricsRetentionHours = 24;
    private const int EventLogRetentionDays = 7;

    public MachineMetricsCleanupJob(
        IMachineMetricsRepository metricsRepository,
        IAgentEventLogRepository eventLogRepository,
        ILogger<MachineMetricsCleanupJob> logger)
    {
        _metricsRepository = metricsRepository;
        _eventLogRepository = eventLogRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting maintenance cleanup job");

        try
        {
            // Machine metrics: 24h retention
            var metricsCutoff = DateTime.UtcNow.AddHours(-MetricsRetentionHours);
            var metricsDeleted = await _metricsRepository.DeleteOlderThanAsync(metricsCutoff);

            // Agent event logs: 7-day retention
            var eventsCutoff = DateTime.UtcNow.AddDays(-EventLogRetentionDays);
            var eventsDeleted = await _eventLogRepository.DeleteOlderThanAsync(eventsCutoff);

            _logger.LogInformation(
                "Maintenance cleanup completed. Metrics: {MetricsCount} rows (>{MetricsHours}h), Events: {EventsCount} rows (>{EventsDays}d)",
                metricsDeleted, MetricsRetentionHours, eventsDeleted, EventLogRetentionDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing maintenance cleanup job");
            throw;
        }
    }
}
