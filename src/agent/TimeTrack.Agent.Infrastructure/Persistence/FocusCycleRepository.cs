using Dapper;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Enums;

namespace TimeTrack.Agent.Infrastructure.Persistence;

/// <summary>
/// Implementação SQLite do repositório de ciclos de foco
///
/// SOLID:
/// - SRP: Apenas persistência de ciclos de foco em SQLite
/// - OCP: Extensível para novas queries sem modificar interface
/// - LSP: Implementa IFocusCycleRepository corretamente
/// - DIP: Depende de SqliteContext (abstração de conexão)
/// </summary>
public sealed class FocusCycleRepository : IFocusCycleRepository
{
    private readonly SqliteContext _context;
    private readonly ILogger<FocusCycleRepository> _logger;

    public FocusCycleRepository(
        SqliteContext context,
        ILogger<FocusCycleRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SaveAsync(FocusCycle cycle, CancellationToken cancellationToken = default)
    {
        if (cycle == null) throw new ArgumentNullException(nameof(cycle));

        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            INSERT OR REPLACE INTO focus_cycles
                (id, user_id, mode, cycle_number, started_at, ended_at,
                 planned_ms, actual_ms, completed, break_taken, synced, date)
            VALUES
                (@Id, @UserId, @Mode, @CycleNumber, @StartedAt, @EndedAt,
                 @PlannedMs, @ActualMs, @Completed, @BreakTaken, @Synced, @Date)
        ";

        await connection.ExecuteAsync(sql, new
        {
            Id = cycle.Id.ToString(),
            UserId = cycle.UserId.ToString(),
            Mode = (int)cycle.Mode,
            CycleNumber = cycle.CycleNumber,
            StartedAt = cycle.StartedAt.ToString("O"),
            EndedAt = cycle.EndedAt?.ToString("O"),
            PlannedMs = cycle.PlannedMs,
            ActualMs = cycle.ActualMs,
            Completed = cycle.Completed ? 1 : 0,
            BreakTaken = cycle.BreakTaken ? 1 : 0,
            Synced = cycle.Synced ? 1 : 0,
            Date = cycle.Date.ToString("yyyy-MM-dd")
        });

        _logger.LogDebug("Focus cycle saved: {CycleId}", cycle.Id);
    }

    public async Task<IReadOnlyList<FocusCycle>> GetByDateAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, user_id, mode, cycle_number, started_at, ended_at,
                   planned_ms, actual_ms, completed, break_taken, synced, date
            FROM focus_cycles
            WHERE user_id = @UserId AND date = @Date
            ORDER BY started_at
        ";

        var dtos = await connection.QueryAsync<FocusCycleDto>(sql, new
        {
            UserId = userId.ToString(),
            Date = date.ToString("yyyy-MM-dd")
        });

        return dtos.Select(MapToDomain).ToList();
    }

    public async Task<FocusCycle?> GetActiveCycleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, user_id, mode, cycle_number, started_at, ended_at,
                   planned_ms, actual_ms, completed, break_taken, synced, date
            FROM focus_cycles
            WHERE user_id = @UserId AND ended_at IS NULL
            ORDER BY started_at DESC
            LIMIT 1
        ";

        var dto = await connection.QueryFirstOrDefaultAsync<FocusCycleDto>(sql, new
        {
            UserId = userId.ToString()
        });

        return dto != null ? MapToDomain(dto) : null;
    }

    public async Task<FocusCycle?> GetLastCompletedCycleAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, user_id, mode, cycle_number, started_at, ended_at,
                   planned_ms, actual_ms, completed, break_taken, synced, date
            FROM focus_cycles
            WHERE user_id = @UserId AND completed = 1
            ORDER BY ended_at DESC
            LIMIT 1
        ";

        var dto = await connection.QueryFirstOrDefaultAsync<FocusCycleDto>(sql, new
        {
            UserId = userId.ToString()
        });

        return dto != null ? MapToDomain(dto) : null;
    }

    public async Task<int> CountCompletedCyclesAsync(
        Guid userId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT COUNT(*)
            FROM focus_cycles
            WHERE user_id = @UserId AND date = @Date AND completed = 1
        ";

        var count = await connection.QueryFirstOrDefaultAsync<int>(sql, new
        {
            UserId = userId.ToString(),
            Date = date.ToString("yyyy-MM-dd")
        });

        return count;
    }

    public async Task<IReadOnlyList<FocusCycle>> GetUnsyncedAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        const string sql = @"
            SELECT id, user_id, mode, cycle_number, started_at, ended_at,
                   planned_ms, actual_ms, completed, break_taken, synced, date
            FROM focus_cycles
            WHERE user_id = @UserId AND synced = 0 AND completed = 1
            ORDER BY ended_at
        ";

        var dtos = await connection.QueryAsync<FocusCycleDto>(sql, new
        {
            UserId = userId.ToString()
        });

        return dtos.Select(MapToDomain).ToList();
    }

    public async Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default)
    {
        await using var gate = await _context.AcquireDbLockAsync(cancellationToken);
        var connection = await _context.GetConnectionAsync(cancellationToken);

        // Only delete cycles that are completed and synced — unsynced cycles must survive until sync
        const string sql = "DELETE FROM focus_cycles WHERE date(started_at) < date(@Cutoff) AND completed = 1 AND synced = 1";
        var deleted = await connection.ExecuteAsync(sql, new { Cutoff = cutoffUtc.ToString("yyyy-MM-dd") });

        if (deleted > 0)
            _logger.LogInformation("Cleaned up {Count} old focus cycles (before {Cutoff:yyyy-MM-dd})", deleted, cutoffUtc);

        return deleted;
    }

    private static FocusCycle MapToDomain(FocusCycleDto dto)
    {
        var cycle = new FocusCycle(
            Guid.Parse(dto.Id),
            Guid.Parse(dto.User_Id),
            (FocusModeType)dto.Mode,
            dto.Cycle_Number,
            dto.Planned_Ms);

        // Restore state using reflection since properties are private set
        if (DateTime.TryParse(dto.Ended_At, out var endedAt))
        {
            cycle.GetType().GetProperty("EndedAt")?.SetValue(cycle, endedAt);
        }

        if (dto.Actual_Ms.HasValue)
        {
            cycle.GetType().GetProperty("ActualMs")?.SetValue(cycle, dto.Actual_Ms.Value);
        }

        if (dto.Completed == 1)
        {
            cycle.Complete();
        }

        if (dto.Break_Taken == 1)
        {
            cycle.MarkBreakTaken();
        }

        if (dto.Synced == 1)
        {
            cycle.MarkSynced();
        }

        return cycle;
    }

    /// <summary>
    /// DTO interno para mapeamento Dapper
    /// </summary>
    private sealed class FocusCycleDto
    {
        public string Id { get; set; } = string.Empty;
        public string User_Id { get; set; } = string.Empty;
        public int Mode { get; set; }
        public int Cycle_Number { get; set; }
        public string Started_At { get; set; } = string.Empty;
        public string? Ended_At { get; set; }
        public int Planned_Ms { get; set; }
        public int? Actual_Ms { get; set; }
        public int Completed { get; set; }
        public int Break_Taken { get; set; }
        public int Synced { get; set; }
        public string Date { get; set; } = string.Empty;
    }
}
