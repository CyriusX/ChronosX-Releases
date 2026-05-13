using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class InviteLinkRepository : IInviteLinkRepository
{
    private readonly TimeTrackDbContext _context;

    public InviteLinkRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<OrgInviteLink?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.OrgInviteLinks
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task<OrgInviteLink?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.OrgInviteLinks
            .IgnoreQueryFilters()
            .Include(l => l.Organization)
            .FirstOrDefaultAsync(l => l.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<List<OrgInviteLink>> ListActiveByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgInviteLinks
            .Where(l => l.OrgId == orgId && l.IsActive)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(OrgInviteLink link, CancellationToken cancellationToken = default)
    {
        await _context.OrgInviteLinks.AddAsync(link, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(OrgInviteLink link, CancellationToken cancellationToken = default)
    {
        _context.OrgInviteLinks.Update(link);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
