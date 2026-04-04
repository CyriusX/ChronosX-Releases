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

        // Prefer the record for this userId; fall back to NULL user_id (not yet migrated via orphan migration)
        // or Guid.Empty user_id (saved before user logged in — pre-login placeholder).
        // This prevents HeartbeatService from incorrectly reporting "stopped" when tracking is active
        // but the state row hasn't been migrated to the real userId yet.
        const string sql = @"
            SELECT id, user_id, status, reason, paused_at, resumed_at, updated_at, last_modified_by
            FROM tracking_state
            WHERE user_id = @UserId OR user_id IS NULL OR user_id = @GuidEmpty
            ORDER BY
                CASE
                    WHEN user_id = @UserId  THEN 0
                    WHEN user_id IS NULL    THEN 1
                    ELSE                        2
                END
            LIMIT 1";

        var dto = await connection.QueryFirstOrDefaultAsync<DapperTrackingStateDto>(
            sql,
            new { UserId = userId.ToString(), GuidEmpty = Guid.Empty.ToString() });

        if (dto == null)
            return null;

        _logger.LogDebug("Tracking state loaded for user {UserId}: status={Status} (row user_id={RowUserId})",
            userId, dto.Status, dto.User_Id);

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
            PausedAt = !string.IsNullOrEmpty(dto.PausedAt) ? DateTime.Parse(dto.PausedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
            ResumedAt = !string.IsNullOrEmpty(dto.ResumedAt) ? DateTime.Parse(dto.ResumedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) : null,
            UpdatedAt = !string.IsNullOrEmpty(dto.UpdatedAt) ? DateTime.Parse(dto.UpdatedAt, null, System.Globalization.DateTimeStyles.RoundtripKind) : DateTime.UtcNow,
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
        public string? PausedAt { get; set; }
        public string? ResumedAt { get; set; }
        public string UpdatedAt { get; set; } = string.Empty;
        public string? LastModifiedBy { get; set; }
    }
}
