using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Aggregates;
using TimeTrack.Agent.Domain.Enums;

// Alias para evitar conflict with local DTO
using DomainTrackingStateDto = TimeTrack.Agent.Domain.Aggregates.TrackingStateDto;

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
        var dto = state.ToDto();

        // SQLite não tem UPSERT nativo elegante, então deleta e insere
        const string deleteSql = "DELETE FROM tracking_state";
        const string insertSql = @"
            INSERT INTO tracking_state (id, status, reason, paused_at, resumed_at, updated_at, last_modified_by)
            VALUES (@Id, @Status, @Reason, @PausedAt, @ResumedAt, @UpdatedAt, @LastModifiedBy)";

        await connection.ExecuteAsync(deleteSql);
        await connection.ExecuteAsync(insertSql, new
        {
            Id = dto.Id.ToString(),
            Status = (int)dto.Status,
            dto.Reason,
            dto.PausedAt,
            dto.ResumedAt,
            dto.UpdatedAt,
            dto.LastModifiedBy
        });

        _logger.LogDebug("Tracking state saved: {Status}", dto.Status);
    }

    private static TrackingState MapToDomain(DapperTrackingStateDto dto)
    {
        return TrackingState.FromDto(new DomainTrackingStateDto
        {
            Id = dto.Id,
            Status = dto.Status,
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
        public Guid Id { get; set; }
        public TrackingStatus Status { get; set; }
        public string? Reason { get; set; }
        public DateTime? PausedAt { get; set; }
        public DateTime? ResumedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? LastModifiedBy { get; set; }
    }
}
