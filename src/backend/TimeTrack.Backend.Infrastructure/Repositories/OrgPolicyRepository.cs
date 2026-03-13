using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for OrgPolicy
/// </summary>
public sealed class OrgPolicyRepository : IOrgPolicyRepository
{
    private readonly TimeTrackDbContext _context;

    public OrgPolicyRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<OrgPolicy?> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<OrgPolicy>()
            .FirstOrDefaultAsync(p => p.OrgId == orgId, cancellationToken);
    }

    public async Task<OrgPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Set<OrgPolicy>()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task AddAsync(OrgPolicy policy, CancellationToken cancellationToken = default)
    {
        await _context.Set<OrgPolicy>().AddAsync(policy, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrgPolicy policy, CancellationToken cancellationToken = default)
    {
        _context.Set<OrgPolicy>().Update(policy);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
