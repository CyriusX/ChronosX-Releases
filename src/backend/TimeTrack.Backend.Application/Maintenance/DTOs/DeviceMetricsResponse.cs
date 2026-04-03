namespace TimeTrack.Backend.Application.Maintenance.DTOs;

/// <summary>
/// Resposta com métricas atuais de um dispositivo
/// </summary>
public sealed class DeviceMetricsResponse
{
    public Guid DeviceId { get; init; }
    public double CpuPercent { get; init; }
    public long MemoryUsedMb { get; init; }
    public long MemoryTotalMb { get; init; }
    public double DiskUsedGb { get; init; }
    public double DiskTotalGb { get; init; }
    public DateTime? LastUpdatedAt { get; init; }
    public IReadOnlyList<MetricsHistoryPoint> RecentHistory { get; init; } = Array.Empty<MetricsHistoryPoint>();
}

/// <summary>
/// Ponto no histórico de métricas para sparklines
/// </summary>
public sealed class MetricsHistoryPoint
{
    public DateTime SampledAt { get; init; }
    public double CpuPercent { get; init; }
    public long MemoryUsedMb { get; init; }
}
