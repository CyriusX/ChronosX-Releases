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

    public async Task<TrackingState?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, user_id, status, reason, paused_at, resumed_at, updated_at, last_modified_by
            FROM tracking_state
            WHERE user_id = @UserId
            LIMIT 1";

        var dto = await connection.QueryFirstOrDefaultAsync<DapperTrackingStateDto>(sql, new { UserId = userId.ToString() });

        if (dto == null)
            return null;

        return MapToDomain(dto);
    }

    public async Task SaveAsync(TrackingState state, CancellationToken cancellationToken = default)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));

        var connection = await _context.GetConnectionAsync(cancellationToken);

        // SQLite doesn't have elegant UPSERT, so delete and insert for this user
        const string deleteSql = "DELETE FROM tracking_state WHERE user_id = @UserId";
        const string insertSql = @"
            INSERT INTO tracking_state (id, user_id, status, reason, paused_at, resumed_at, updated_at, last_modified_by)
            VALUES (@Id, @UserId, @Status, @Reason, @PausedAt, @ResumedAt, @UpdatedAt, @LastModifiedBy)";

        await connection.ExecuteAsync(deleteSql, new { UserId = state.UserId.ToString() });
        await connection.ExecuteAsync(insertSql, new
        {
            Id = state.Id.ToString(),
            UserId = state.UserId.ToString(),
            Status = (int)state.Status,
            Reason = state.Reason,
            PausedAt = state.PausedAt?.ToString() ?? null,
            ResumedAt = state.ResumedAt?.ToString() ?? null,
            UpdatedAt = state.UpdatedAt.ToString("O"),
            LastModifiedBy = state.LastModifiedBy
        });

        _logger.LogDebug("Tracking state saved for user {UserId}: {Status}", state.UserId, state.Status);
    }

    private static TrackingState? MapToDomain(DapperTrackingStateDto dto)
    {
        if (dto == null)
            return null;

        return TrackingState.FromDto(new TrackingStateDto
        {
            Id = Guid.Parse(dto.Id),
            UserId = Guid.Parse(dto.User_Id),
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
        public string Id { get; set; } = string.Empty;
        public string User_Id { get; set; } = string.Empty;
        public int Status { get; set; }
        public string? Reason { get; set; }
        public DateTime? PausedAt { get; set; }
        public DateTime? ResumedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LastModifiedBy { get; set; }
    }
}
