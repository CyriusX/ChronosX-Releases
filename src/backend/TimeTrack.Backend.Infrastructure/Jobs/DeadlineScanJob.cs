using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Notifications;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class DeadlineScanJob : IDeadlineScanJob
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IAgentNotificationInboxRepository _inbox;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<DeadlineScanJob> _logger;

    public DeadlineScanJob(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IAgentNotificationInboxRepository inbox,
        INotificationDispatcher dispatcher,
        ILogger<DeadlineScanJob> logger)
    {
        _tasks = tasks;
        _projects = projects;
        _inbox = inbox;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        var today = DateTime.UtcNow.Date;
        _logger.LogInformation("DeadlineScanJob starting for {Date:yyyy-MM-dd}", today);

        var dueTasks = await _tasks.ListAssignedTasksDueOnAsync(today, CancellationToken.None);
        if (dueTasks.Count == 0)
        {
            _logger.LogInformation("DeadlineScanJob: no tasks due today");
            return;
        }

        var dispatched = 0;
        var skipped = 0;
        var failed = 0;
        var projectCache = new Dictionary<Guid, Domain.Entities.Project>();

        foreach (var task in dueTasks)
        {
            if (!task.AssignedUserId.HasValue) continue;

            try
            {
                var already = await _inbox.HasNotificationForTaskOnDayAsync(
                    task.AssignedUserId.Value,
                    AgentNotificationKind.DeadlineToday,
                    task.Id,
                    today,
                    CancellationToken.None);

                if (already)
                {
                    skipped++;
                    continue;
                }

                if (!projectCache.TryGetValue(task.ProjectId, out var project))
                {
                    project = task.Project ?? await _projects.GetByIdAsync(task.ProjectId, CancellationToken.None);
                    if (project is null)
                    {
                        _logger.LogWarning("DeadlineScanJob: task {TaskId} has no project, skipping", task.Id);
                        continue;
                    }
                    projectCache[task.ProjectId] = project;
                }

                await _dispatcher.NotifyDeadlineTodayAsync(task.AssignedUserId.Value, task, project, CancellationToken.None);
                dispatched++;
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "DeadlineScanJob: failed to dispatch deadline notification for task {TaskId}", task.Id);
            }
        }

        _logger.LogInformation(
            "DeadlineScanJob done. Tasks due today: {Total}, dispatched: {Dispatched}, already sent: {Skipped}, failed: {Failed}",
            dueTasks.Count, dispatched, skipped, failed);
    }
}
