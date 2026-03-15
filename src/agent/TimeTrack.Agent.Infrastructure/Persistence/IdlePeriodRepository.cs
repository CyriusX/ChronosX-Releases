using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Implementação SQLite do repositório de períodos de inatividade
/// </summary>
public sealed class IdlePeriodRepository : IIdlePeriodRepository
{
    private readonly SqliteContext _context;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<IdlePeriodRepository> _logger;

    public IdlePeriodRepository(
        SqliteContext context,
        IOutboxRepository outboxRepository,
        ILogger<IdlePeriodRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _outboxRepository = outboxRepository ?? throw new ArgumentNullException(nameof(outboxRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<IdlePeriod>> GetByDateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await GetByDateRangeAsync(userId, startOfDay, endOfDay, cancellationToken);
    }

    public async Task<IReadOnlyList<IdlePeriod>> GetByDateRangeAsync(
        Guid userId,
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, user_id, start_utc, end_utc, threshold_seconds, is_system_detected
            FROM idle_periods
            WHERE user_id = @UserId AND start_utc >= @Start AND start_utc < @End
            ORDER BY start_utc";

        var dtos = await connection.QueryAsync<IdlePeriodDto>(sql, new { UserId = userId.ToString(), Start = start, End = end });

        return dtos.Select(MapToDomain).ToList();
    }

    public async Task SaveAsync(IdlePeriod period, CancellationToken cancellationToken = default)
    {
        if (period == null) throw new ArgumentNullException(nameof(period));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT OR REPLACE INTO idle_periods
                (id, user_id, start_utc, end_utc, threshold_seconds, is_system_detected)
            VALUES
                (@Id, @UserId, @StartUtc, @EndUtc, @ThresholdSeconds, @IsSystemDetected)";

        await connection.ExecuteAsync(sql, new
        {
            Id = period.Id.ToString(),
            UserId = period.UserId.ToString(),
            StartUtc = period.Period.StartUtc,
            EndUtc = period.Period.EndUtc,
            ThresholdSeconds = period.ThresholdSeconds,
            IsSystemDetected = period.IsSystemDetected ? 1 : 0
        });

        _logger.LogDebug("Idle period saved: {Period}", period);
    }

    public async Task SaveBatchAsync(
        IEnumerable<IdlePeriod> periods,
        CancellationToken cancellationToken = default)
    {
        if (periods == null) throw new ArgumentNullException(nameof(periods));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT OR REPLACE INTO idle_periods
                (id, user_id, start_utc, end_utc, threshold_seconds, is_system_detected)
            VALUES
                (@Id, @UserId, @StartUtc, @EndUtc, @ThresholdSeconds, @IsSystemDetected)";

        var parameters = periods.Select(p => new
        {
            Id = p.Id.ToString(),
            UserId = p.UserId.ToString(),
            StartUtc = p.Period.StartUtc,
            EndUtc = p.Period.EndUtc,
            ThresholdSeconds = p.ThresholdSeconds,
            IsSystemDetected = p.IsSystemDetected ? 1 : 0
        });

        await connection.ExecuteAsync(sql, parameters);

        _logger.LogDebug("Batch of {Count} idle periods saved", parameters.Count());
    }

    public async Task SaveWithOutboxAsync(
        IdlePeriod period,
        IEnumerable<OutboxItem> outboxItems,
        CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);
        var transaction = await _context.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Salvar idle period
            const string periodSql = @"
                INSERT OR REPLACE INTO idle_periods
                    (id, user_id, start_utc, end_utc, threshold_seconds, is_system_detected)
                VALUES
                    (@Id, @UserId, @StartUtc, @EndUtc, @ThresholdSeconds, @IsSystemDetected)
            ";

            await connection.ExecuteAsync(periodSql, new
            {
                Id = period.Id.ToString(),
                UserId = period.UserId.ToString(),
                StartUtc = period.Period.StartUtc,
                EndUtc = period.Period.EndUtc,
                ThresholdSeconds = period.ThresholdSeconds,
                IsSystemDetected = period.IsSystemDetected ? 1 : 0
            });

            // 2. Salvar outbox items
            const string outboxSql = @"
                INSERT OR IGNORE INTO sync_outbox
                    (id, user_id, entity_type, entity_id, payload_json, idempotency_key,
                     attempt_count, next_attempt_utc, sent_at, last_error, created_at)
                VALUES
                    (@Id, @UserId, @EntityType, @EntityId, @PayloadJson, @IdempotencyKey,
                     @AttemptCount, @NextAttemptUtc, @SentAt, @LastError, @CreatedAt)
            ";

            var itemList = outboxItems.ToList();
            foreach (var item in itemList)
            {
                _logger.LogDebug(
                    "Saving outbox item for idle period: Id={Id}, EntityType={EntityType}, EntityId={EntityId}",
                    item.Id, item.EntityType, item.EntityId);

                await connection.ExecuteAsync(outboxSql, new
                {
                    Id = item.Id.ToString(),
                    UserId = period.UserId.ToString(),
                    EntityType = item.EntityType,
                    EntityId = item.EntityId.ToString(),
                    PayloadJson = item.PayloadJson,
                    IdempotencyKey = item.IdempotencyKey,
                    AttemptCount = item.AttemptCount,
                    NextAttemptUtc = item.NextAttemptUtc?.ToString("o"),
                    SentAt = item.SentAt?.ToString("o"),
                    LastError = item.LastError,
                    CreatedAt = item.CreatedAt.ToString("o")
                });
            }

            await transaction.CommitAsync();

            _logger.LogDebug("Idle period {PeriodId} saved with {Count} outbox items", period.Id, itemList.Count);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to save idle period with outbox items");
            throw;
        }
    }

    private static IdlePeriod MapToDomain(IdlePeriodDto dto)
    {
        var period = new TimeRange(dto.Start_Utc, dto.End_Utc);

        return new IdlePeriod(
            Guid.Parse(dto.Id),
            Guid.Parse(dto.User_Id),
            period,
            dto.Threshold_Seconds,
            dto.Is_System_Detected != 0);
    }

    /// <summary>
    /// DTO interno para mapeamento Dapper
    /// </summary>
    private sealed class IdlePeriodDto
    {
        public string Id { get; set; } = string.Empty;
        public string User_Id { get; set; } = string.Empty;
        public DateTime Start_Utc { get; set; }
        public DateTime End_Utc { get; set; }
        public int Threshold_Seconds { get; set; }
        public int Is_System_Detected { get; set; }
    }
}
