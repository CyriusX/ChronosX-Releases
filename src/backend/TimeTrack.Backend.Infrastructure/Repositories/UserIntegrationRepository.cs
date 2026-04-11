using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class UserIntegrationRepository : IUserIntegrationRepository
{
    private readonly TimeTrackDbContext _context;

    public UserIntegrationRepository(TimeTrackDbContext context) => _context = context;

    public Task<UserIntegration?> GetAsync(Guid userId, UserIntegrationProvider provider, CancellationToken ct = default)
        => _context.UserIntegrations
            .FirstOrDefaultAsync(i => i.UserId == userId && i.Provider == provider, ct);

    public async Task<IReadOnlyList<UserIntegration>> ListByUserAsync(Guid userId, CancellationToken ct = default)
        => await _context.UserIntegrations
            .Where(i => i.UserId == userId)
            .OrderBy(i => i.Provider)
            .ToListAsync(ct);

    public async Task AddAsync(UserIntegration integration, CancellationToken ct = default)
    {
        await _context.UserIntegrations.AddAsync(integration, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserIntegration integration, CancellationToken ct = default)
    {
        _context.UserIntegrations.Update(integration);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(UserIntegration integration, CancellationToken ct = default)
    {
        _context.UserIntegrations.Remove(integration);
        await _context.SaveChangesAsync(ct);
    }
}
