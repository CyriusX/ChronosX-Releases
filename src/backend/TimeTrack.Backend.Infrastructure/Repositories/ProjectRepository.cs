using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class ProjectRepository : IProjectRepository
{
    private readonly TimeTrackDbContext _context;

    public ProjectRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects.FindAsync([id], cancellationToken);
    }

    public async Task<Project?> GetByIdWithOrgAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Include(p => p.Organization)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Where(p => p.OrgId == orgId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Project>> GetActiveByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Where(p => p.OrgId == orgId && p.Status == ProjectStatus.Active)
            .OrderBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> NameExistsInOrgAsync(string name, Guid orgId, Guid? excludeProjectId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Projects
            .Where(p => p.OrgId == orgId && p.Name.ToLower() == name.ToLower());

        if (excludeProjectId.HasValue)
        {
            query = query.Where(p => p.Id != excludeProjectId.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task AddAsync(Project project, CancellationToken cancellationToken = default)
    {
        await _context.Projects.AddAsync(project, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Remove(project);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
