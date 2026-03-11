using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Aggregates;
using TimeTrack.Agent.Domain.Enums;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Implementação SQLite do repositório de estado de tracking
/// </summary>
public sealed class TrackingStateRepository : ITrackingStateRepository
{
    private readonly SqliteContext _context;
    private readonly ILogger<TrackingStateRepository> _logger;

    public TrackingStateRepository(
        SqliteContext context,
        ILogger<TrackingStateRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TrackingState?> GetAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, status, reason, paused_at, resumed_at, updated_at, last_modified_by
            FROM tracking_state
            LIMIT 1";

        var dto = await connection.QueryFirstOrDefaultAsync<DapperTrackingStateDto>(sql);

        if (dto == null)
            return null;

        return MapToDomain(dto);
    }

    public async Task SaveAsync(TrackingState state, CancellationToken cancellationToken = default)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        // SQLite doesn't have elegant UPSERT, so delete and insert
        const string deleteSql = "DELETE FROM tracking_state";
        const string insertSql = @"
            INSERT INTO tracking_state (id, status, reason, paused_at, resumed_at, updated_at, last_modified_by)
            VALUES (@Id, @Status, @Reason, @PausedAt, @ResumedAt, @UpdatedAt, @LastModifiedBy)";

        await connection.ExecuteAsync(deleteSql);
        await connection.ExecuteAsync(insertSql, new
        {
            Id = state.Id.ToString(),
            Status = (int)state.Status,
            Reason = state.Reason,
            PausedAt = state.PausedAt?.ToString() ?? null,
            ResumedAt = state.ResumedAt?.ToString() ?? null,
            UpdatedAt = state.UpdatedAt.ToString("O"),
            LastModifiedBy = state.LastModifiedBy
        });

        _logger.LogDebug("Tracking state saved: {Status}", state.Status);
    }

    private static TrackingState? MapToDomain(DapperTrackingStateDto dto)
    {
        if (dto == null)
            return null;

        return TrackingState.FromDto(new TrackingStateDto
        {
            Id = Guid.Parse(dto.Id),
            Status = (TrackingStatus)dto.Status,
            Reason = dto.Reason,
            PausedAt = dto.PausedAt,
            ResumedAt = dto.ResumedAt,
            UpdatedAt = dto.UpdatedAt,
            LastModifiedBy = dto.LastModifiedBy
        });
    }

    /// <summary>
    /// DTO interno para mapeamento Dapper (nomes de colunas mapeados)
    /// </summary>
    private sealed class DapperTrackingStateDto
    {
        public string Id { get; set; }  // Store as string in SQLite
        public int Status { get; set; }  // Keep as int for Dapper mapping
        public string? Reason { get; set; }
        public DateTime? PausedAt { get; set; }
        public DateTime? ResumedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LastModifiedBy { get; set; }
    }
}
