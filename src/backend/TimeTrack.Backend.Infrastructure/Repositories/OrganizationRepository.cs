using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly TimeTrackDbContext _context;

    public OrganizationRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<Organization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Organizations.ToListAsync(cancellationToken);
    }

    public async Task<Organization?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .FirstOrDefaultAsync(o => o.Slug == slug.ToLowerInvariant(), cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Organizations
            .AnyAsync(o => o.Slug == slug.ToLowerInvariant(), cancellationToken);
    }

    public async Task AddAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        await _context.Organizations.AddAsync(organization, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _context.Organizations.Update(organization);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Organization organization, CancellationToken cancellationToken = default)
    {
        _context.Organizations.Remove(organization);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
