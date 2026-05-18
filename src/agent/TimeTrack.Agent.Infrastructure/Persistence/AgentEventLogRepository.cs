using Dapper;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Repositório de eventos do agent em SQLite
/// </summary>
public sealed class AgentEventLogRepository : IAgentEventLogRepository
{
    private readonly SqliteContext _context;

    public AgentEventLogRepository(SqliteContext context)
    {
        _context = context;
    }

    public async Task AddAsync(AgentEventLog eventLog, CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            INSERT INTO agent_event_log (id, event_type, category, severity, message, metadata_json, timestamp_utc)
            VALUES (@Id, @EventType, @Category, @Severity, @Message, @MetadataJson, @TimestampUtc)
            """;

        await connection.ExecuteAsync(sql, new
        {
            eventLog.Id,
            eventLog.EventType,
            eventLog.Category,
            eventLog.Severity,
            eventLog.Message,
            eventLog.MetadataJson,
            eventLog.TimestampUtc
        });
    }

    public async Task<IReadOnlyList<AgentEventLog>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            SELECT id AS Id, event_type AS EventType, category AS Category,
                   severity AS Severity, message AS Message, metadata_json AS MetadataJson,
                   timestamp_utc AS TimestampUtc
            FROM agent_event_log
            ORDER BY timestamp_utc DESC
            LIMIT @Limit
            """;

        var results = await connection.QueryAsync<AgentEventLog>(sql, new { Limit = limit });
        return results.ToList();
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = """
            DELETE FROM agent_event_log
            WHERE timestamp_utc < @Cutoff
            """;

        return await connection.ExecuteAsync(sql, new { Cutoff = cutoff.ToString("O") });
    }
}
