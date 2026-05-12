namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Interface para a fila local de upload de evidências
/// </summary>
public interface IEvidenceUploadQueue
{
    Task EnqueueAsync(EvidenceQueueItem item, CancellationToken ct = default);
    Task<IReadOnlyList<EvidenceQueueItem>> GetPendingAsync(int limit, CancellationToken ct = default);
    Task MarkAsUploadedAsync(string id, CancellationToken ct = default);
    Task MarkAsFailedAsync(string id, string error, CancellationToken ct = default);
    Task CleanupOldEntriesAsync(int olderThanDays, CancellationToken ct = default);
}

public sealed class EvidenceQueueItem
{
    public required string Id { get; init; }
    public required string LocalPath { get; init; }
    public required string EvidenceType { get; init; }
    public required DateTime CapturedAt { get; init; }
    public required string AppName { get; init; }
    public string? WindowTitleHash { get; init; }
    public int AttemptCount { get; set; }
    public DateTime NextAttemptUtc { get; set; }
    public DateTime? UploadedAt { get; set; }
    public long FileSizeBytes { get; init; }
    public string Status { get; set; } = "pending"; // pending, uploading, uploaded, failed
}
