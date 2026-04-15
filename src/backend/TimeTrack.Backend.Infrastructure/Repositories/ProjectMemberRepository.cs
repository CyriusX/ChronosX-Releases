using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class ProjectMemberRepository : IProjectMemberRepository
{
    private readonly TimeTrackDbContext _context;

    public ProjectMemberRepository(TimeTrackDbContext context) => _context = context;

    public Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => _context.ProjectMembers.FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

    public Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken ct = default)
        => _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == userId, ct);

    public async Task<IReadOnlyList<ProjectMember>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
        => await _context.ProjectMembers
            .Include(m => m.User)
            .Where(m => m.ProjectId == projectId)
            .OrderBy(m => m.AddedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListProjectIdsForUserAsync(Guid userId, CancellationToken ct = default)
        => await _context.ProjectMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ProjectId)
            .ToListAsync(ct);

    public async Task AddAsync(ProjectMember member, CancellationToken ct = default)
    {
        await _context.ProjectMembers.AddAsync(member, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(ProjectMember member, CancellationToken ct = default)
    {
        _context.ProjectMembers.Remove(member);
        await _context.SaveChangesAsync(ct);
    }
}
