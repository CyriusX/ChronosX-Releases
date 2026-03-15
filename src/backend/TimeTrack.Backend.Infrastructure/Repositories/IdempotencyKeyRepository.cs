using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class IdempotencyKeyRepository : IIdempotencyKeyRepository
{
    private readonly TimeTrackDbContext _context;

    public IdempotencyKeyRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<IdempotencyKey?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyKeys.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<IdempotencyKey>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyKeys.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(IdempotencyKey entity, CancellationToken cancellationToken = default)
    {
        await _context.IdempotencyKeys.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(IdempotencyKey entity, CancellationToken cancellationToken = default)
    {
        _context.IdempotencyKeys.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(IdempotencyKey entity, CancellationToken cancellationToken = default)
    {
        _context.IdempotencyKeys.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IdempotencyKey?> GetByKeyAsync(
        string key,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyKeys
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(k => k.Key == key && k.EntityType == entityType, cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        string key,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyKeys
            .IgnoreQueryFilters()
            .AnyAsync(k => k.Key == key && k.EntityType == entityType, cancellationToken);
    }

    public async Task<HashSet<string>> GetExistingKeysAsync(
        IEnumerable<string> keys,
        string entityType,
        CancellationToken cancellationToken = default)
    {
        var keyList = keys.ToList();
        var existingKeys = await _context.IdempotencyKeys
            .IgnoreQueryFilters()
            .Where(k => k.EntityType == entityType && keyList.Contains(k.Key))
            .Select(k => k.Key)
            .ToListAsync(cancellationToken);

        return existingKeys.ToHashSet();
    }

    public async Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default)
    {
        var expired = await _context.IdempotencyKeys
            .IgnoreQueryFilters()
            .Where(k => k.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        _context.IdempotencyKeys.RemoveRange(expired);
        await _context.SaveChangesAsync(cancellationToken);

        return expired.Count;
    }
}
