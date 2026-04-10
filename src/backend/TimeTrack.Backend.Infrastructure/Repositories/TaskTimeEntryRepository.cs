using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class TaskTimeEntryRepository : ITaskTimeEntryRepository
{
    private readonly TimeTrackDbContext _context;

    public TaskTimeEntryRepository(TimeTrackDbContext context) => _context = context;

    public Task<TaskTimeEntry?> GetOpenForUserAsync(Guid userId, CancellationToken ct = default)
        => _context.TaskTimeEntries
            .Include(e => e.Task)
                .ThenInclude(t => t!.Project)
            .FirstOrDefaultAsync(e => e.UserId == userId && e.EndedAt == null, ct);

    public async Task<IReadOnlyList<TaskTimeEntry>> ListForUserOnDateAsync(Guid userId, DateOnly date, CancellationToken ct = default)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = startUtc.AddDays(1);
        return await ListForUserInRangeAsync(userId, startUtc, endUtc, ct);
    }

    public async Task<IReadOnlyList<TaskTimeEntry>> ListForUserInRangeAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken ct = default)
        => await _context.TaskTimeEntries
            .Include(e => e.Task)
                .ThenInclude(t => t!.Project)
            .Where(e => e.UserId == userId && e.StartedAt < endUtc && (e.EndedAt == null || e.EndedAt > startUtc))
            .OrderBy(e => e.StartedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TaskTimeEntry>> ListForTaskAsync(Guid taskId, CancellationToken ct = default)
        => await _context.TaskTimeEntries
            .Where(e => e.TaskId == taskId)
            .OrderBy(e => e.StartedAt)
            .ToListAsync(ct);

    public async Task AddAsync(TaskTimeEntry entry, CancellationToken ct = default)
    {
        await _context.TaskTimeEntries.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(TaskTimeEntry entry, CancellationToken ct = default)
    {
        _context.TaskTimeEntries.Update(entry);
        await _context.SaveChangesAsync(ct);
    }
}
