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
        var connection = await _context.GetConnectionAsync(cancellationToken);

        // Count errors in the last hour as a proxy for consecutive failures
        const string sql = """
            SELECT COUNT(*)
            FROM sync_errors
            WHERE timestamp_utc > datetime('now', '-1 hour')
            """;

        var count = await connection.ExecuteScalarAsync<int>(sql);
        return count;
    }

    public async Task CleanupOldErrorsAsync(int retentionDays = 30, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            DELETE FROM sync_errors
            WHERE timestamp_utc < datetime('now', @RetentionDays || ' days')
            """;

        await connection.ExecuteAsync(sql, new { RetentionDays = -retentionDays });
    }
}
