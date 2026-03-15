using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de sessões de foco
///
/// SOLID:
/// - SRP: Apenas persistência de sessões de foco
/// - OCP: Extensível para novas queries sem modificar interface
/// - LSP: Implementa IFocusSessionRepository corretamente
/// - DIP: Depende de abstrações (IFocusSessionRepository, TimeTrackDbContext)
/// </summary>
public sealed class FocusSessionRepository : IFocusSessionRepository
{
    private readonly TimeTrackDbContext _context;

    public FocusSessionRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<FocusSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.FocusSessions.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<FocusSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.FocusSessions.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(FocusSession entity, CancellationToken cancellationToken = default)
    {
        await _context.FocusSessions.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FocusSession entity, CancellationToken cancellationToken = default)
    {
        _context.FocusSessions.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FocusSession entity, CancellationToken cancellationToken = default)
    {
        _context.FocusSessions.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<FocusSession>> GetByUserIdAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.FocusSessions
            .Where(f => f.UserId == userId && f.StartedAt >= startOfDay && f.StartedAt <= endOfDay)
            .OrderByDescending(f => f.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FocusSession>> GetByUserIdAndDateRangeAsync(
        Guid userId,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var start = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.FocusSessions
            .Where(f => f.UserId == userId && f.StartedAt >= start && f.StartedAt <= end)
            .OrderByDescending(f => f.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<FocusSession>> GetByOrgIdAndDateAsync(
        Guid orgId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        return await _context.FocusSessions
            .Where(f => f.OrgId == orgId && f.StartedAt >= startOfDay && f.StartedAt <= endOfDay)
            .OrderByDescending(f => f.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        return await _context.FocusSessions
            .AnyAsync(f => f.IdempotencyKey == idempotencyKey, cancellationToken);
    }

    public async Task<int> GetTotalFocusMinutesByUserAndDateAsync(
        Guid userId,
        DateOnly date,
        CancellationToken cancellationToken = default)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var sessions = await _context.FocusSessions
            .Where(f => f.UserId == userId &&
                        f.StartedAt >= startOfDay &&
                        f.StartedAt <= endOfDay &&
                        f.Status == FocusSessionStatus.Completed)
            .Select(f => f.ActualDurationMinutes ?? 0)
            .ToListAsync(cancellationToken);

        return sessions.Sum();
    }
}
