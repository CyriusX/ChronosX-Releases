namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Snapshot de métricas de máquina (CPU, Memória, Disco) enviado pelo Agent
/// </summary>
public sealed class MachineMetrics
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public double CpuPercent { get; private set; }
    public long MemoryUsedMb { get; private set; }
    public long MemoryTotalMb { get; private set; }
    public double DiskUsedGb { get; private set; }
    public double DiskTotalGb { get; private set; }
    public DateTime SampledAtUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Navigation
    public Device? Device { get; private set; }

    private MachineMetrics() { }

    public static MachineMetrics Create(
        Guid id,
        Guid orgId,
        Guid deviceId,
        double cpuPercent,
        long memoryUsedMb,
        long memoryTotalMb,
        double diskUsedGb,
        double diskTotalGb,
        DateTime sampledAtUtc,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        return new MachineMetrics
        {
            Id = id,
            OrgId = orgId,
            DeviceId = deviceId,
            CpuPercent = Math.Clamp(cpuPercent, 0, 100),
            MemoryUsedMb = memoryUsedMb,
            MemoryTotalMb = memoryTotalMb,
            DiskUsedGb = diskUsedGb,
            DiskTotalGb = diskTotalGb,
            SampledAtUtc = sampledAtUtc,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }
}
