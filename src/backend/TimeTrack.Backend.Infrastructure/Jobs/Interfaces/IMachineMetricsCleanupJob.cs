namespace TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

/// <summary>
/// Job de limpeza de métricas de máquina antigas (retenção de 24h)
/// </summary>
public interface IMachineMetricsCleanupJob
{
    Task ExecuteAsync();
}
