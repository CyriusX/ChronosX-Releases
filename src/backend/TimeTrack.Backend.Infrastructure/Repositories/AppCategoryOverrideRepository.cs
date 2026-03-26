using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;
// Using IdentifierNormalizer for normalization

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for organization-specific category overrides
///
/// SRP: Apenas implementa operações de dados para overrides
/// DIP: Implementa interface definida no Domain
/// </summary>
public sealed class AppCategoryOverrideRepository : IAppCategoryOverrideRepository
{
    private readonly TimeTrackDbContext _context;

    public AppCategoryOverrideRepository(TimeTrackDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<AppCategoryOverride?> FindAsync(
        Guid orgId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = IdentifierNormalizer.Normalize(identifier);
        return await _context.AppCategoryOverrides
            .AsNoTracking()
            .FirstOrDefaultAsync(
                o => o.OrgId == orgId && o.Identifier == normalizedIdentifier,
                cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryOverride>> GetByOrgIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppCategoryOverrides
            .AsNoTracking()
            .Where(o => o.OrgId == orgId)
            .OrderBy(o => o.Identifier)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryOverride>> GetByOrgAndProductivityAsync(
        Guid orgId,
        AppProductivityCategory productivity,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppCategoryOverrides
            .AsNoTracking()
            .Where(o => o.OrgId == orgId && o.Productivity == productivity)
            .OrderBy(o => o.Identifier)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AppCategoryOverride>> GetByOrgAndIdentifiersAsync(
        Guid orgId,
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifiers = identifiers
            .Select(id => IdentifierNormalizer.Normalize(id))
            .ToHashSet();

        return await _context.AppCategoryOverrides
            .AsNoTracking()
            .Where(o => o.OrgId == orgId && normalizedIdentifiers.Contains(o.Identifier))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(
        Guid orgId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = IdentifierNormalizer.Normalize(identifier);
        return await _context.AppCategoryOverrides
            .AnyAsync(
                o => o.OrgId == orgId && o.Identifier == normalizedIdentifier,
                cancellationToken);
    }

    public async Task<AppCategoryOverride> AddAsync(
        AppCategoryOverride categoryOverride,
        CancellationToken cancellationToken = default)
    {
        _context.AppCategoryOverrides.Add(categoryOverride);
        await _context.SaveChangesAsync(cancellationToken);
        return categoryOverride;
    }

    public async Task UpdateAsync(
        AppCategoryOverride categoryOverride,
        CancellationToken cancellationToken = default)
    {
        _context.AppCategoryOverrides.Update(categoryOverride);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.AppCategoryOverrides
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (entity == null)
            return false;

        _context.AppCategoryOverrides.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeleteByOrgAndIdentifierAsync(
        Guid orgId,
        string identifier,
        CancellationToken cancellationToken = default)
    {
        var normalizedIdentifier = IdentifierNormalizer.Normalize(identifier);
        var entity = await _context.AppCategoryOverrides
            .FirstOrDefaultAsync(
                o => o.OrgId == orgId && o.Identifier == normalizedIdentifier,
                cancellationToken);

        if (entity == null)
            return false;

        _context.AppCategoryOverrides.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> CountByOrgIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AppCategoryOverrides
            .CountAsync(o => o.OrgId == orgId, cancellationToken);
    }
}
