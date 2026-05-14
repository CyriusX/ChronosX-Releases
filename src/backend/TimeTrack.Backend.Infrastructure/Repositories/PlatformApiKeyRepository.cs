using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class PlatformApiKeyRepository : IPlatformApiKeyRepository
{
    private readonly TimeTrackDbContext _context;

    public PlatformApiKeyRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformApiKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PlatformApiKeys.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<PlatformApiKey>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PlatformApiKeys
            .OrderByDescending(k => k.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlatformApiKey entity, CancellationToken cancellationToken = default)
    {
        await _context.PlatformApiKeys.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PlatformApiKey entity, CancellationToken cancellationToken = default)
    {
        _context.PlatformApiKeys.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(PlatformApiKey entity, CancellationToken cancellationToken = default)
    {
        _context.PlatformApiKeys.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlatformApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyHash)) return null;

        return await _context.PlatformApiKeys
            .FirstOrDefaultAsync(k => k.KeyHash == keyHash, cancellationToken);
    }
}

