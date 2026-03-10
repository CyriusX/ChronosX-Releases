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
    private readonly ILogger<IdlePeriodRepository> _logger;

    public IdlePeriodRepository(
        SqliteContext context,
        ILogger<IdlePeriodRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<IdlePeriod>> GetByDateAsync(
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.Date;
        var endOfDay = startOfDay.AddDays(1);

        return await GetByDateRangeAsync(startOfDay, endOfDay, cancellationToken);
    }

    public async Task<IReadOnlyList<IdlePeriod>> GetByDateRangeAsync(
        DateTime start,
        DateTime end,
        CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, start_utc, end_utc, threshold_seconds, is_system_detected
            FROM idle_periods
            WHERE start_utc >= @Start AND start_utc < @End
            ORDER BY start_utc";

        var dtos = await connection.QueryAsync<IdlePeriodDto>(sql, new { Start = start, End = end });

        return dtos.Select(MapToDomain).ToList();
    }

    public async Task SaveAsync(IdlePeriod period, CancellationToken cancellationToken = default)
    {
        if (period == null) throw new ArgumentNullException(nameof(period));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT OR REPLACE INTO idle_periods
                (id, start_utc, end_utc, threshold_seconds, is_system_detected)
            VALUES
                (@Id, @StartUtc, @EndUtc, @ThresholdSeconds, @IsSystemDetected)";

        await connection.ExecuteAsync(sql, new
        {
            Id = period.Id.ToString(),
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
                (id, start_utc, end_utc, threshold_seconds, is_system_detected)
            VALUES
                (@Id, @StartUtc, @EndUtc, @ThresholdSeconds, @IsSystemDetected)";

        var parameters = periods.Select(p => new
        {
            Id = p.Id.ToString(),
            StartUtc = p.Period.StartUtc,
            EndUtc = p.Period.EndUtc,
            ThresholdSeconds = p.ThresholdSeconds,
            IsSystemDetected = p.IsSystemDetected ? 1 : 0
        });

        await connection.ExecuteAsync(sql, parameters);

        _logger.LogDebug("Batch of {Count} idle periods saved", parameters.Count());
    }

    private static IdlePeriod MapToDomain(IdlePeriodDto dto)
    {
        var period = new TimeRange(dto.Start_Utc, dto.End_Utc);

        return new IdlePeriod(
            Guid.Parse(dto.Id),
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
        public DateTime Start_Utc { get; set; }
        public DateTime End_Utc { get; set; }
        public int Threshold_Seconds { get; set; }
        public int Is_System_Detected { get; set; }
    }
}
