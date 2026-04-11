using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class LinearSyncHistoryRepository : ILinearSyncHistoryRepository
{
    private readonly TimeTrackDbContext _context;

    public LinearSyncHistoryRepository(TimeTrackDbContext context) => _context = context;

    public async Task<IReadOnlyList<LinearSyncHistory>> ListRecentForUserAsync(Guid userId, int take = 20, CancellationToken ct = default)
        => await _context.LinearSyncHistory
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.StartedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task AddAsync(LinearSyncHistory entry, CancellationToken ct = default)
    {
        await _context.LinearSyncHistory.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }
}
