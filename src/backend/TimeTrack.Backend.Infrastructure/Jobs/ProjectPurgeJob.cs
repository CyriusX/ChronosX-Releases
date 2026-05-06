using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Purges projects that were soft-deleted more than 30 days ago.
/// </summary>
public sealed class ProjectPurgeJob : IProjectPurgeJob
{
    private readonly TimeTrackDbContext _context;
    private readonly ILogger<ProjectPurgeJob> _logger;

    private const int BatchSize = 200;
    private const int PurgeAfterDays = 30;

    public ProjectPurgeJob(TimeTrackDbContext context, ILogger<ProjectPurgeJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task ExecuteAsync() => ExecuteInternalAsync(CancellationToken.None);

    private async Task ExecuteInternalAsync(CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-PurgeAfterDays);
        _logger.LogInformation("Starting project purge job at {Time}. Cutoff={Cutoff}", DateTime.UtcNow, cutoff);

        var totalPurged = 0;

        while (true)
        {
            var projects = await _context.Projects
                .IgnoreQueryFilters()
                .Where(p => p.DeletedAt != null && p.DeletedAt < cutoff)
                .OrderBy(p => p.Id)
                .Take(BatchSize)
                .ToListAsync(ct);

            if (projects.Count == 0)
                break;

            var projectIds = projects.Select(p => p.Id).ToList();

            // Clear activity links to avoid "ghost references" in timelines/reports.
            var sessionsByProject = await _context.ActivitySessions
                .IgnoreQueryFilters()
                .Where(s => s.ProjectId != null && projectIds.Contains(s.ProjectId.Value))
                .ToListAsync(ct);

            foreach (var s in sessionsByProject)
                s.ClearTaskLink();

            var taskIds = await _context.ProjectTasks
                .IgnoreQueryFilters()
                .Where(t => projectIds.Contains(t.ProjectId))
                .Select(t => t.Id)
                .ToListAsync(ct);

            if (taskIds.Count > 0)
            {
                var sessionsByTask = await _context.ActivitySessions
                    .IgnoreQueryFilters()
                    .Where(s => s.TaskId != null && taskIds.Contains(s.TaskId.Value))
                    .ToListAsync(ct);

                foreach (var s in sessionsByTask)
                    s.ClearTaskLink();
            }

            await _context.SaveChangesAsync(ct);

            _context.Projects.RemoveRange(projects);
            await _context.SaveChangesAsync(ct);

            totalPurged += projects.Count;

            if (projects.Count < BatchSize)
                break;
        }

        _logger.LogInformation("Project purge job completed. Purged {Count} projects.", totalPurged);
    }
}

