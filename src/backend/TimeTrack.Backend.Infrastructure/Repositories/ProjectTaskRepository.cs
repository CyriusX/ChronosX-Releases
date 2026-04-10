using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class ProjectTaskRepository : IProjectTaskRepository
{
    private readonly TimeTrackDbContext _context;

    public ProjectTaskRepository(TimeTrackDbContext context) => _context = context;

    public Task<ProjectTask?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.ProjectTasks
            .Include(t => t.AssignedUser)
            .Include(t => t.Project)
            .FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<ProjectTask>> ListByProjectAsync(Guid projectId, CancellationToken ct = default)
        => await _context.ProjectTasks
            .Include(t => t.AssignedUser)
            .Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Position)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ProjectTask>> ListAssignedToUserAsync(Guid userId, bool includeDone = false, CancellationToken ct = default)
    {
        var query = _context.ProjectTasks
            .Include(t => t.Project)
            .Where(t => t.AssignedUserId == userId);

        if (!includeDone)
            query = query.Where(t => t.Status != ProjectTaskStatus.Done);

        return await query
            .OrderBy(t => t.Status)
            .ThenBy(t => t.Position)
            .ToListAsync(ct);
    }

    public async Task<double> GetMaxPositionInColumnAsync(Guid projectId, ProjectTaskStatus status, CancellationToken ct = default)
    {
        var max = await _context.ProjectTasks
            .Where(t => t.ProjectId == projectId && t.Status == status)
            .Select(t => (double?)t.Position)
            .MaxAsync(ct);
        return max ?? 0;
    }

    public async Task AddAsync(ProjectTask task, CancellationToken ct = default)
    {
        await _context.ProjectTasks.AddAsync(task, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ProjectTask task, CancellationToken ct = default)
    {
        _context.ProjectTasks.Update(task);
        await _context.SaveChangesAsync(ct);
    }
}
