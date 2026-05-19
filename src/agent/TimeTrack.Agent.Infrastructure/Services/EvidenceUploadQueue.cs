using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.Persistence;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Fila local de upload de evidências usando SQLite.
/// Separada do sync_outbox principal para não interferir com o SyncWorker.
/// </summary>
public sealed class EvidenceUploadQueue : IEvidenceUploadQueue
{
    private readonly SqliteContext _sqlite;
    private readonly ILogger<EvidenceUploadQueue> _logger;

    public EvidenceUploadQueue(SqliteContext sqlite, ILogger<EvidenceUploadQueue> logger)
    {
        _sqlite = sqlite;
        _logger = logger;
    }

    public async Task EnqueueAsync(EvidenceQueueItem item, CancellationToken ct = default)
    {
        var conn = await _sqlite.GetConnectionAsync(ct);
        await Dapper.SqlMapper.ExecuteAsync(conn, @"
            INSERT INTO evidence_upload_queue
                (id, local_path, evidence_type, captured_at, app_name, window_title_hash,
                 attempt_count, next_attempt_utc, uploaded_at, file_size_bytes, status, created_at)
            VALUES
                (@Id, @LocalPath, @EvidenceType, @CapturedAt, @AppName, @WindowTitleHash,
                 @AttemptCount, @NextAttemptUtc, @UploadedAt, @FileSizeBytes, @Status, datetime('now'))",
            new
            {
                item.Id,
                item.LocalPath,
                item.EvidenceType,
                CapturedAt = item.CapturedAt.ToString("o"),
                item.AppName,
                item.WindowTitleHash,
                item.AttemptCount,
                NextAttemptUtc = item.NextAttemptUtc.ToString("o"),
                UploadedAt = item.UploadedAt?.ToString("o"),
                item.FileSizeBytes,
                item.Status
            });

        _logger.LogDebug("Enqueued evidence upload: {Id}, type={Type}", item.Id, item.EvidenceType);
    }

    private static readonly int MaxBackoffMinutes = 30;

    public async Task<IReadOnlyList<EvidenceQueueItem>> GetPendingAsync(int limit, CancellationToken ct = default)
    {
        var conn = await _sqlite.GetConnectionAsync(ct);
        var rows = await Dapper.SqlMapper.QueryAsync<dynamic>(conn, @"
            SELECT id, local_path, evidence_type, captured_at, app_name, window_title_hash,
                   attempt_count, next_attempt_utc, uploaded_at, file_size_bytes, status
            FROM evidence_upload_queue
            WHERE status IN ('pending', 'failed')
              AND datetime(next_attempt_utc) <= datetime('now')
            ORDER BY captured_at ASC
            LIMIT @Limit",
            new { Limit = limit });

        return rows.Select(r => new EvidenceQueueItem
        {
            Id = (string)r.id,
            LocalPath = (string)r.local_path,
            EvidenceType = (string)r.evidence_type,
            CapturedAt = DateTime.Parse((string)r.captured_at),
            AppName = (string)r.app_name,
            WindowTitleHash = (string?)r.window_title_hash,
            AttemptCount = (int)r.attempt_count,
            NextAttemptUtc = DateTime.Parse((string)r.next_attempt_utc),
            UploadedAt = r.uploaded_at != null ? DateTime.Parse((string)r.uploaded_at) : null,
            FileSizeBytes = (long)r.file_size_bytes,
            Status = (string)r.status
        }).ToList();
    }

    public async Task MarkAsUploadedAsync(string id, CancellationToken ct = default)
    {
        var conn = await _sqlite.GetConnectionAsync(ct);
        await Dapper.SqlMapper.ExecuteAsync(conn, @"
            UPDATE evidence_upload_queue
            SET status = 'uploaded', uploaded_at = datetime('now')
            WHERE id = @Id",
            new { Id = id });

        _logger.LogInformation("Evidence upload marked as uploaded: {Id}", id);
    }

    public async Task MarkAsFailedAsync(string id, string error, CancellationToken ct = default)
    {
        var conn = await _sqlite.GetConnectionAsync(ct);

        var item = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<dynamic>(conn,
            "SELECT attempt_count FROM evidence_upload_queue WHERE id = @Id", new { Id = id });

        if (item == null) return;

        var attemptCount = (int)item.attempt_count + 1;
        var backoffMinutes = Math.Min(
            (int)Math.Pow(2, Math.Min(attemptCount - 1, 5)),
            MaxBackoffMinutes);

        var nextAttempt = DateTime.UtcNow.AddMinutes(backoffMinutes);
        await Dapper.SqlMapper.ExecuteAsync(conn, @"
            UPDATE evidence_upload_queue
            SET attempt_count = @AttemptCount,
                next_attempt_utc = @NextAttempt,
                status = 'pending'
            WHERE id = @Id",
            new { Id = id, AttemptCount = attemptCount, NextAttempt = nextAttempt.ToString("o") });

        _logger.LogWarning("Evidence upload attempt {Attempt} failed for {Id}. Retry at {NextAttempt}. Error: {Error}",
            attemptCount, id, nextAttempt, error);
    }

    public async Task CleanupOldEntriesAsync(int olderThanDays, CancellationToken ct = default)
    {
        var conn = await _sqlite.GetConnectionAsync(ct);
        var deleted = await Dapper.SqlMapper.ExecuteAsync(conn, @"
            DELETE FROM evidence_upload_queue
            WHERE status = 'uploaded'
              AND created_at < datetime('now', '-' || @Days || ' days')",
            new { Days = olderThanDays });

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} old evidence queue entries", deleted);
    }
}
