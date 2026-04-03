using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IMachineMetricsRepository : IRepository<MachineMetrics>
{
    /// <summary>
    /// Obtém a métrica mais recente de um dispositivo
    /// </summary>
    Task<MachineMetrics?> GetLatestByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtém métricas de um dispositivo a partir de uma data
    /// </summary>
    Task<IEnumerable<MachineMetrics>> GetByDeviceIdSinceAsync(
        Guid deviceId,
        DateTime since,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove métricas anteriores ao cutoff (retenção de dados)
    /// </summary>
    Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);
}
