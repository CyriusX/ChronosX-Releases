using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Integrations.DTOs;
using TimeTrack.Backend.Application.Integrations.Linear.Services;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Integrations.Linear.Commands;

public sealed record SyncFromLinearCommand : IRequest<LinearSyncResultResponse>;

/// <summary>
/// Pulls every project and assigned issue for the connected Linear account
/// and mirrors them into the local database. Existing Linear-sourced rows
/// are updated in place; missing ones are soft-deleted.
///
/// The handler deliberately writes task status via ApplyLinearSnapshot
/// (which does NOT open a TaskTimeEntry) — we're reflecting what Linear
/// already says, not starting a new work session for the user.
/// </summary>
public sealed class SyncFromLinearCommandHandler : IRequestHandler<SyncFromLinearCommand, LinearSyncResultResponse>
{
    private readonly IUserIntegrationRepository _integrations;
    private readonly ILinearSyncHistoryRepository _history;
    private readonly IProjectRepository _projects;
    private readonly IProjectTaskRepository _tasks;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ILinearClient _linear;
    private readonly IUserIntegrationTokenProtector _protector;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<SyncFromLinearCommandHandler> _logger;

    public SyncFromLinearCommandHandler(
        IUserIntegrationRepository integrations,
        ILinearSyncHistoryRepository history,
        IProjectRepository projects,
        IProjectTaskRepository tasks,
        ITaskTimeEntryRepository entries,
        ILinearClient linear,
        IUserIntegrationTokenProtector protector,
        ICurrentUserContext currentUser,
        ILogger<SyncFromLinearCommandHandler> logger)
    {
        _integrations = integrations;
        _history = history;
        _projects = projects;
        _tasks = tasks;
        _entries = entries;
        _linear = linear;
        _protector = protector;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<LinearSyncResultResponse> Handle(SyncFromLinearCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException();

        var userId = _currentUser.UserId.Value;
        var orgId = _currentUser.OrgId.Value;
        var startedAt = DateTime.UtcNow;

        var integration = await _integrations.GetAsync(userId, UserIntegrationProvider.Linear, ct)
            ?? throw new NotFoundException("UserIntegration", "Linear");

        if (integration.Status == UserIntegrationStatus.Revoked)
            throw new ValidationException("Integration", "Linear integration was revoked. Please reconnect.");

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(integration.EncryptedToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt Linear token for user {UserId}", userId);
            throw new ValidationException("Integration", "Stored Linear token is corrupt. Please reconnect.");
        }

        // ── Fetch from Linear ────────────────────────────────────────────
        IReadOnlyList<LinearIssue> issues;
        try
        {
            issues = await _linear.GetMyIssuesAsync(apiKey, ct);
        }
        catch (LinearUnauthorizedException ex)
        {
            integration.MarkUnauthorized(ex.Message);
            await _integrations.UpdateAsync(integration, ct);
            await RecordFailureAsync(orgId, userId, startedAt, "Linear rejected the API key (reconnect required)", ct);
            throw new ValidationException("Integration", "Linear rejected the API key. Please reconnect.");
        }
        catch (LinearApiException ex)
        {
            await RecordFailureAsync(orgId, userId, startedAt, ex.Message, ct);
            throw new ValidationException("Integration", $"Linear sync failed: {ex.Message}");
        }

        // ── Project upsert ───────────────────────────────────────────────
        var projectsCreated = 0;
        var projectsUpdated = 0;
        var projectsByLinearId = new Dictionary<string, Project>();

        var distinctLinearProjects = issues
            .Where(i => i.Project is not null)
            .GroupBy(i => i.Project!.Id)
            .Select(g => g.First().Project!)
            .ToList();

        foreach (var lp in distinctLinearProjects)
        {
            var existing = await _projects.GetByLinearProjectIdAsync(orgId, lp.Id, ct);
            if (existing is null)
            {
                var project = Project.CreateFromLinear(
                    orgId,
                    lp.Name,
                    ResolveProjectColor(lp.Id),
                    lp.Id,
                    linearWorkspaceId: null,
                    description: null);
                await _projects.AddAsync(project, ct);
                projectsByLinearId[lp.Id] = project;
                projectsCreated++;
            }
            else
            {
                existing.ApplyLinearSnapshot(lp.Name, existing.Color, existing.Description);
                await _projects.UpdateAsync(existing, ct);
                projectsByLinearId[lp.Id] = existing;
                projectsUpdated++;
            }
        }

        // ── Task upsert ──────────────────────────────────────────────────
        var tasksCreated = 0;
        var tasksUpdated = 0;
        var tasksSoftDeleted = 0;
        var seenIssueIdsByProject = new Dictionary<Guid, HashSet<string>>();

        foreach (var issue in issues)
        {
            if (issue.Project is null)
            {
                // Skip orphan issues — per plan decision, we only mirror issues with a project.
                continue;
            }

            if (!projectsByLinearId.TryGetValue(issue.Project.Id, out var localProject))
                continue; // shouldn't happen, but be defensive

            var mappedStatus = LinearStateMapper.FromLinear(issue.State.Name, issue.State.Type);
            var mappedPriority = LinearPriorityMapper.FromLinear(issue.Priority);
            var description = issue.Description is { Length: > 2000 }
                ? issue.Description[..2000]
                : issue.Description;

            var existing = await _tasks.GetByLinearIssueIdAsync(orgId, issue.Id, ct);
            if (existing is not null && existing.DeletedAt is not null)
            {
                // Issue was previously soft-deleted (e.g. removed from Linear then re-assigned).
                // Restore it so the unique index is not violated on the update below.
                existing.Restore();
            }

            if (existing is null)
            {
                var task = ProjectTask.CreateFromLinear(
                    orgId,
                    localProject.Id,
                    userId,
                    issue.Title,
                    description,
                    mappedPriority,
                    issue.DueDate,
                    mappedStatus,
                    userId, // Linear filters by "assigned to me"
                    position: 1024,
                    linearIssueId: issue.Id,
                    linearIssueIdentifier: issue.Identifier,
                    linearUrl: issue.Url,
                    linearStateId: issue.State.Id,
                    linearStateName: issue.State.Name,
                    linearTeamId: issue.Team.Id);
                await _tasks.AddAsync(task, ct);
                tasksCreated++;
            }
            else
            {
                // If the snapshot moves the task out of InProgress, close any open timer first
                // so accumulated seconds are preserved and the timer doesn't become orphaned.
                if (existing.Status == ProjectTaskStatus.InProgress && mappedStatus != ProjectTaskStatus.InProgress)
                {
                    var open = await _entries.GetOpenForUserAsync(userId, ct);
                    if (open is not null && open.TaskId == existing.Id)
                    {
                        var added = open.Close();
                        await _entries.UpdateAsync(open, ct);
                        if (added > 0)
                            existing.AccumulateWorkedTime(added);
                    }
                }

                existing.ApplyLinearSnapshot(
                    issue.Title,
                    description,
                    mappedPriority,
                    issue.DueDate,
                    mappedStatus,
                    userId,
                    issue.Identifier,
                    issue.Url,
                    issue.State.Id,
                    issue.State.Name,
                    issue.Team.Id);
                await _tasks.UpdateAsync(existing, ct);
                tasksUpdated++;
            }

            if (!seenIssueIdsByProject.TryGetValue(localProject.Id, out var set))
            {
                set = new HashSet<string>();
                seenIssueIdsByProject[localProject.Id] = set;
            }
            set.Add(issue.Id);
        }

        // ── Orphan cleanup: soft-delete Linear tasks no longer present ───
        foreach (var (projectId, seenIds) in seenIssueIdsByProject)
        {
            var localLinearTasks = await _tasks.ListLinearTasksForProjectAsync(projectId, ct);
            foreach (var task in localLinearTasks)
            {
                if (task.LinearIssueId is null) continue;
                if (task.DeletedAt is not null) continue;
                if (seenIds.Contains(task.LinearIssueId)) continue;

                // Close an open timer on the orphan before soft-deleting
                if (task.Status == ProjectTaskStatus.InProgress && task.AssignedUserId.HasValue)
                {
                    var open = await _entries.GetOpenForUserAsync(task.AssignedUserId.Value, ct);
                    if (open is not null && open.TaskId == task.Id)
                    {
                        open.Close();
                        await _entries.UpdateAsync(open, ct);
                    }
                }

                task.SoftDelete();
                await _tasks.UpdateAsync(task, ct);
                tasksSoftDeleted++;
            }
        }

        // ── Retry any pending pushes that failed during earlier MoveTask calls ──
        await RetryPendingPushesAsync(integration, apiKey, ct);
        LinearPendingPushQueue.Prune(integration);

        // ── Finalize ─────────────────────────────────────────────────────
        integration.MarkSynced();
        integration.MarkUsed();
        await _integrations.UpdateAsync(integration, ct);

        var finishedAt = DateTime.UtcNow;
        var history = LinearSyncHistory.CreateSuccess(
            orgId, userId, startedAt, finishedAt,
            projectsCreated, projectsUpdated, tasksCreated, tasksUpdated, tasksSoftDeleted);
        await _history.AddAsync(history, ct);

        _logger.LogInformation(
            "Linear sync for {UserId} done. projects +{PC}/~{PU}, tasks +{TC}/~{TU}/-{TD}, took {Ms}ms",
            userId, projectsCreated, projectsUpdated, tasksCreated, tasksUpdated, tasksSoftDeleted, history.DurationMs);

        return new LinearSyncResultResponse
        {
            ProjectsCreated = projectsCreated,
            ProjectsUpdated = projectsUpdated,
            TasksCreated = tasksCreated,
            TasksUpdated = tasksUpdated,
            TasksSoftDeleted = tasksSoftDeleted,
            DurationMs = history.DurationMs,
            StartedAt = startedAt,
            FinishedAt = finishedAt,
            Integration = IntegrationMapper.Map(integration)
        };
    }

    // Linear projects don't always have a usable color. Pick a stable fallback
    // derived from the project id so repeated syncs produce the same color.
    private static string ResolveProjectColor(string linearProjectId)
    {
        var palette = new[] { "#4A9FFF", "#a855f7", "#05df72", "#fb923c", "#f87171", "#fbbf24", "#60a5fa" };
        var hash = 0;
        foreach (var c in linearProjectId) hash = (hash * 31 + c) & 0x7FFFFFFF;
        return palette[hash % palette.Length];
    }

    private async Task RecordFailureAsync(Guid orgId, Guid userId, DateTime startedAt, string error, CancellationToken ct)
    {
        var row = LinearSyncHistory.CreateFailure(orgId, userId, startedAt, DateTime.UtcNow, error);
        try { await _history.AddAsync(row, ct); }
        catch (Exception logEx) { _logger.LogWarning(logEx, "Could not persist linear sync failure history"); }
    }

    /// <summary>
    /// Walks the pending-push queue persisted on the integration metadata and
    /// re-attempts each one. The push path in <c>MoveTaskCommandHandler</c>
    /// enqueues entries when the live push failed; this is our chance to
    /// recover them without running a dedicated Hangfire worker.
    /// </summary>
    private async Task RetryPendingPushesAsync(Domain.Entities.UserIntegration integration, string apiKey, CancellationToken ct)
    {
        var pending = LinearPendingPushQueue.List(integration);
        if (pending.Count == 0) return;

        _logger.LogInformation("Retrying {Count} pending Linear push(es) for user {UserId}",
            pending.Count, integration.UserId);

        foreach (var entry in pending)
        {
            var task = await _tasks.GetByIdAsync(entry.TaskId, ct);
            if (task is null || !task.IsLinearSourced || string.IsNullOrWhiteSpace(task.LinearTeamId) || string.IsNullOrWhiteSpace(task.LinearIssueId))
            {
                // Task gone or no longer Linear-sourced — drop the entry.
                LinearPendingPushQueue.Dequeue(integration, entry.TaskId);
                continue;
            }

            // Only retry if the local status still matches the target that was queued;
            // otherwise the user has moved the card elsewhere and the live MoveTask
            // handler will own the fresh push.
            if (task.Status != entry.TargetStatus)
            {
                LinearPendingPushQueue.Dequeue(integration, entry.TaskId);
                continue;
            }

            // Load team states (cached if fresh)
            var now = DateTime.UtcNow;
            IReadOnlyList<LinearWorkflowState>? teamStates =
                LinearTeamStateCache.TryGet(integration, task.LinearTeamId!, now);
            if (teamStates is null)
            {
                try
                {
                    teamStates = await _linear.GetTeamStatesAsync(apiKey, task.LinearTeamId!, ct);
                    LinearTeamStateCache.Store(integration, task.LinearTeamId!, teamStates, now);
                }
                catch (Exception ex)
                {
                    LinearPendingPushQueue.Enqueue(integration, task.Id, entry.TargetStatus, ex.Message);
                    continue;
                }
            }

            var target = LinearStateMapper.ResolveLinearState(entry.TargetStatus, teamStates);
            if (target is null)
            {
                LinearPendingPushQueue.Enqueue(integration, task.Id, entry.TargetStatus, "no_linear_state_match");
                continue;
            }

            try
            {
                var ok = await _linear.UpdateIssueStateAsync(apiKey, task.LinearIssueId!, target.Id, ct);
                if (ok)
                {
                    task.UpdateLinearState(target.Id, target.Name);
                    await _tasks.UpdateAsync(task, ct);
                    LinearPendingPushQueue.Dequeue(integration, task.Id);
                }
                else
                {
                    LinearPendingPushQueue.Enqueue(integration, task.Id, entry.TargetStatus, "issue_update_returned_false");
                }
            }
            catch (LinearUnauthorizedException ex)
            {
                integration.MarkUnauthorized(ex.Message);
                LinearPendingPushQueue.Enqueue(integration, task.Id, entry.TargetStatus, "unauthorized");
                // No point retrying the rest of the queue with a bad token.
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retry push failed for task {TaskId}", task.Id);
                LinearPendingPushQueue.Enqueue(integration, task.Id, entry.TargetStatus, ex.Message);
            }
        }
    }
}
