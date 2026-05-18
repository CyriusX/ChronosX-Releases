using Dapper;
using Microsoft.Data.Sqlite;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Repositório de erros de sincronização em SQLite
/// </summary>
public sealed class SyncErrorRepository : ISyncErrorRepository
{
    private readonly SqliteContext _context;

    public SyncErrorRepository(SqliteContext context)
    {
        _context = context;
    }

    public async Task AddAsync(SyncError error, CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO sync_errors (id, timestamp_utc, endpoint, status_code, error_message, attempt_count)
            VALUES (@Id, @TimestampUtc, @Endpoint, @StatusCode, @ErrorMessage, @AttemptCount)
            """;

        await connection.ExecuteAsync(sql, new
        {
            error.Id,
            error.TimestampUtc,
            error.Endpoint,
            error.StatusCode,
            error.ErrorMessage,
            error.AttemptCount
        });
    }

    public async Task<SyncError?> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            SELECT id AS Id, timestamp_utc AS TimestampUtc, endpoint AS Endpoint,
                   status_code AS StatusCode, error_message AS ErrorMessage, attempt_count AS AttemptCount
            FROM sync_errors
            ORDER BY timestamp_utc DESC
            LIMIT 1
            """;

        return await connection.QueryFirstOrDefaultAsync<SyncError>(sql);
    }

    public async Task<IEnumerable<SyncError>> GetByDateRangeAsync(
        DateTime from,
        DateTime to,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            SELECT id AS Id, timestamp_utc AS TimestampUtc, endpoint AS Endpoint,
                   status_code AS StatusCode, error_message AS ErrorMessage, attempt_count AS AttemptCount
            FROM sync_errors
            WHERE timestamp_utc >= @From AND timestamp_utc <= @To
            ORDER BY timestamp_utc DESC
            """;

        return await connection.QueryAsync<SyncError>(sql, new { From = from, To = to });
    }

    public async Task<int> CountConsecutiveFailuresAsync(CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        // Count only errors that occurred AFTER the most recent successful sync.
        // When any sync succeeds, sent_at is stamped on outbox rows, so MAX(sent_at)
        // moves forward and all older errors fall outside the window → count resets to 0.
        // This correctly clears the failure count as soon as the agent recovers.
        const string sql = """
            SELECT COUNT(*)
            FROM sync_errors
            WHERE timestamp_utc > COALESCE(
                (SELECT MAX(sent_at) FROM sync_outbox WHERE sent_at IS NOT NULL),
                '1970-01-01T00:00:00'
            )
            """;

        var count = await connection.ExecuteScalarAsync<int>(sql);
        return count;
    }

    public async Task CleanupOldErrorsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            DELETE FROM sync_errors
            WHERE timestamp_utc < datetime('now', @RetentionDays || ' days')
            """;

        await connection.ExecuteAsync(sql, new { RetentionDays = -retentionDays });
    }
}
