using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Implementação SQLite do repositório Outbox
/// </summary>
public sealed class OutboxRepository : IOutboxRepository
{
    private readonly SqliteContext _context;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly ILogger<OutboxRepository> _logger;
    private readonly OutboxBackoffConfig _backoffConfig;

    public OutboxRepository(
        SqliteContext context,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        ILogger<OutboxRepository> logger,
        IOptions<OutboxBackoffConfig>? options = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _idempotencyKeyGenerator = idempotencyKeyGenerator ?? throw new ArgumentNullException(nameof(idempotencyKeyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _backoffConfig = options?.Value ?? new OutboxBackoffConfig();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxItem>> GetPendingAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
            throw new ArgumentException("Limit must be at least 1", nameof(limit));

        var connection = await _context.GetConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("o");

        const string sql = @"
            SELECT id, entity_type, entity_id, payload_json, idempotency_key,
                   attempt_count, next_attempt_utc, sent_at, last_error, created_at
            FROM sync_outbox
            WHERE sent_at IS NULL
              AND next_attempt_utc <= @Now
            ORDER BY next_attempt_utc, id
            LIMIT @Limit";

        var items = (await connection.QueryAsync<OutboxItemDto>(sql, new { Now = now, Limit = limit }))
            .ToList();

        // Filter out items with invalid data and log warnings
        var validItems = new List<OutboxItem>();
        foreach (var dto in items)
        {
            try
            {
                validItems.Add(MapToDomain(dto));
            }
            catch (FormatException ex)
            {
                _logger.LogWarning("Skipping outbox item with invalid data: {Id}, Error: {Error}", dto.Id, ex.Message);
            }
        }

        return validItems;
    }

    /// <inheritdoc />
    public async Task AddAsync(OutboxItem item, CancellationToken cancellationToken = default)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO sync_outbox
                (id, entity_type, entity_id, payload_json, idempotency_key,
                 attempt_count, next_attempt_utc, sent_at, last_error, created_at)
            VALUES
                (@Id, @EntityType, @EntityId, @PayloadJson, @IdempotencyKey,
                 @AttemptCount, @NextAttemptUtc, @SentAt, @LastError, @CreatedAt)";

        await connection.ExecuteAsync(sql, MapToDto(item));
        _logger.LogDebug("Outbox item added: {Id}, EntityType={EntityType}", item.Id, item.EntityType);
    }

    /// <inheritdoc />
    public async Task AddBatchAsync(IEnumerable<OutboxItem> items, CancellationToken cancellationToken = default)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT INTO sync_outbox
                (id, entity_type, entity_id, payload_json, idempotency_key,
                 attempt_count, next_attempt_utc, sent_at, last_error, created_at)
            VALUES
                (@Id, @EntityType, @EntityId, @PayloadJson, @IdempotencyKey,
                 @AttemptCount, @NextAttemptUtc, @SentAt, @LastError, @CreatedAt)";

        var itemList = items.ToList();
        foreach (var item in itemList)
        {
            await connection.ExecuteAsync(sql, MapToDto(item));
        }

        _logger.LogDebug("Batch of {Count} outbox items added", itemList.Count);
    }

    /// <inheritdoc />
    public async Task MarkAsSentAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null) throw new ArgumentNullException(nameof(ids));

        var idList = ids.ToList();
        if (!idList.Any()) return;

        var connection = await _context.GetConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("o");
        var idStrings = idList.Select(id => id.ToString()).ToList();

        const string sql = @"
            UPDATE sync_outbox
            SET sent_at = @SentAt, last_error = NULL
            WHERE id IN @Ids";

        await connection.ExecuteAsync(sql, new { SentAt = now, Ids = idStrings });
        _logger.LogDebug("MarkAsSentAsync: {Count} items marked as sent", idList.Count);
    }

    /// <inheritdoc />
    public async Task MarkAsFailedAsync(Guid id, string error, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        // Primeiro, obter o attempt_count atual
        const string getCountSql = "SELECT attempt_count FROM sync_outbox WHERE id = @Id";
        var currentAttempt = await connection.QueryFirstOrDefaultAsync<int>(getCountSql, new { Id = id.ToString() });

        var nextAttempt = CalculateNextAttempt(currentAttempt + 1);

        const string sql = @"
            UPDATE sync_outbox
            SET sent_at = NULL,
                last_error = @Error,
                attempt_count = attempt_count + 1,
                next_attempt_utc = @NextAttemptUtc
            WHERE id = @Id";

        await connection.ExecuteAsync(sql, new
        {
            Id = id.ToString(),
            Error = error,
            NextAttemptUtc = nextAttempt.ToString("o")
        });

        _logger.LogDebug("MarkAsFailedAsync: {Id}, next_attempt={NextAttempt}", id, nextAttempt);
    }

    /// <inheritdoc />
    public async Task<int> RemoveSentOlderThanAsync(TimeSpan olderThan, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.Subtract(olderThan).ToString("o");

        const string sql = @"
            DELETE FROM sync_outbox
            WHERE sent_at IS NOT NULL
              AND created_at < @Cutoff";

        var deleted = await connection.ExecuteAsync(sql, new { Cutoff = cutoff });
        _logger.LogDebug("Removed {Count} old sent outbox items", deleted);
        return deleted;
    }

    /// <inheritdoc />
    public async Task<OutboxItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, entity_type, entity_id, payload_json, idempotency_key,
                   attempt_count, next_attempt_utc, sent_at, last_error, created_at
            FROM sync_outbox
            WHERE id = @Id";

        var dto = await connection.QueryFirstOrDefaultAsync<OutboxItemDto>(sql, new { Id = id.ToString() });
        return dto != null ? MapToDomain(dto) : null;
    }

    /// <inheritdoc />
    public async Task<bool> HasPendingItemsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("o");

        const string sql = @"
            SELECT COUNT(*)
            FROM sync_outbox
            WHERE sent_at IS NULL
              AND next_attempt_utc <= @Now";

        var count = await connection.ExecuteScalarAsync<int>(sql, new { Now = now });
        return count > 0;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OutboxItem>> GetSentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, entity_type, entity_id, payload_json, idempotency_key,
                   attempt_count, next_attempt_utc, sent_at, last_error, created_at
            FROM sync_outbox
            WHERE sent_at IS NOT NULL
            ORDER BY sent_at DESC
            LIMIT @Limit";

        var items = (await connection.QueryAsync<OutboxItemDto>(sql, new { Limit = limit }))
            .ToList();

        return items.Select(MapToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<int> ResetStuckItemsAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);
        var now = DateTime.UtcNow.ToString("o");

        // Redefine itens que ainda não foram enviados e cujo next_attempt_utc está no futuro
        const string sql = @"
            UPDATE sync_outbox
            SET next_attempt_utc = @Now,
                attempt_count = 0,
                last_error = NULL
            WHERE sent_at IS NULL
              AND next_attempt_utc > @Now";

        var updated = await connection.ExecuteAsync(sql, new { Now = now });

        if (updated > 0)
        {
            _logger.LogWarning(
                "ResetStuckItemsAsync: {Count} itens do outbox redefinidos para tentativa imediata.",
                updated);
        }
        else
        {
            _logger.LogDebug("ResetStuckItemsAsync: nenhum item preso encontrado.");
        }

        return updated;
    }

    private DateTime CalculateNextAttempt(int attemptCount)
    {
        // Backoff exponencial: 1min, 2min, 4min, 8min, 16min, max 30min
        var delayMinutes = Math.Min(Math.Pow(2, attemptCount), 30);
        return DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    #region DTO Mapping

    private sealed class OutboxItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string entity_type { get; set; } = string.Empty;
        public string entity_id { get; set; } = string.Empty;
        public string payload_json { get; set; } = string.Empty;
        public string idempotency_key { get; set; } = string.Empty;
        public int attempt_count { get; set; }
        public string? next_attempt_utc { get; set; }
        public string? sent_at { get; set; }
        public string? last_error { get; set; }
        public string created_at { get; set; } = string.Empty;
    }

    private static OutboxItem MapToDomain(OutboxItemDto dto)
    {
        // Handle empty or invalid GUID strings (SQLite stores as strings)
        if (string.IsNullOrWhiteSpace(dto.entity_id) || !Guid.TryParse(dto.entity_id, out var entityId))
        {
            throw new FormatException($"Invalid EntityId format in outbox item: '{dto.entity_id}'");
        }

        return OutboxItem.Create(
            dto.entity_type,
            entityId,
            dto.payload_json,
            dto.idempotency_key);
    }

    private static object MapToDto(OutboxItem item)
    {
        return new
        {
            Id = item.Id.ToString(),
            EntityType = item.EntityType,
            EntityId = item.EntityId.ToString(),
            PayloadJson = item.PayloadJson,
            IdempotencyKey = item.IdempotencyKey,
            AttemptCount = item.AttemptCount,
            NextAttemptUtc = item.NextAttemptUtc?.ToString("o"),
            SentAt = item.SentAt?.ToString("o"),
            LastError = item.LastError,
            CreatedAt = item.CreatedAt.ToString("o")
        };
    }

    #endregion
}
