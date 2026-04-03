namespace TimeTrack.Agent.Contracts.Providers;

/// <summary>
/// Leitura instantânea de métricas da máquina (CPU, Memória, Disco)
/// </summary>
public sealed record MachineMetricsReading
{
    /// <summary>
    /// Uso de CPU do sistema (0–100)
    /// </summary>
    public required double CpuPercent { get; init; }

    /// <summary>
    /// Memória física utilizada em MB
    /// </summary>
    public required long MemoryUsedMb { get; init; }

    /// <summary>
    /// Memória física total em MB
    /// </summary>
    public required long MemoryTotalMb { get; init; }

    /// <summary>
    /// Espaço em disco utilizado em GB (drives fixos somados)
    /// </summary>
    public required double DiskUsedGb { get; init; }

    /// <summary>
    /// Espaço em disco total em GB (drives fixos somados)
    /// </summary>
    public required double DiskTotalGb { get; init; }

    /// <summary>
    /// Timestamp UTC da coleta
    /// </summary>
    public required DateTime SampledAtUtc { get; init; }
}

/// <summary>
/// Interface para coleta de métricas de hardware da máquina
/// </summary>
public interface IMachineMetricsProvider
{
    /// <summary>
    /// Coleta métricas atuais de CPU, memória e disco
    /// </summary>
    Task<MachineMetricsReading> GetCurrentAsync(CancellationToken cancellationToken = default);
}
