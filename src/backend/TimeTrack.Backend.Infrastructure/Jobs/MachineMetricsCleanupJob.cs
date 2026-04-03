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
    private readonly ILogger<MachineMetricsCleanupJob> _logger;

    private const int RetentionHours = 24;

    public MachineMetricsCleanupJob(
        IMachineMetricsRepository metricsRepository,
        ILogger<MachineMetricsCleanupJob> logger)
    {
        _metricsRepository = metricsRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting machine metrics cleanup job");

        try
        {
            var cutoff = DateTime.UtcNow.AddHours(-RetentionHours);
            var deleted = await _metricsRepository.DeleteOlderThanAsync(cutoff);

            _logger.LogInformation(
                "Machine metrics cleanup completed. Deleted {Count} rows older than {Hours}h",
                deleted, RetentionHours);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing machine metrics cleanup job");
            throw;
        }
    }
}
