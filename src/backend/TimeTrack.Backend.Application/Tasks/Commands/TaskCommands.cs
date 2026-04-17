using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Integrations;
using TimeTrack.Backend.Application.Integrations.Linear;
using TimeTrack.Backend.Application.Integrations.Linear.Services;
using TimeTrack.Backend.Application.Notifications;
using TimeTrack.Backend.Application.Tasks.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Tasks.Commands;

// ═══════════════════════════════════════════════════════════════════════════
// CREATE TASK
// ═══════════════════════════════════════════════════════════════════════════

public sealed record CreateTaskCommand(
    Guid ProjectId,
    string Title,
    string? Description,
    Guid? AssignedUserId,
    string Priority,
    DateTime? DueDate) : IRequest<TaskResponse>;

public sealed class CreateTaskCommandHandler : IRequestHandler<CreateTaskCommand, TaskResponse>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IProjectTaskRepository _tasks;
    private readonly IUserRepository _users;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationDispatcher _notifications;

    public CreateTaskCommandHandler(
        IProjectRepository projects,
        IProjectMemberRepository members,
        IProjectTaskRepository tasks,
        IUserRepository users,
        ICurrentUserContext currentUser,
        INotificationDispatcher notifications)
    {
        _projects = projects;
        _members = members;
        _tasks = tasks;
        _users = users;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<TaskResponse> Handle(CreateTaskCommand request, CancellationToken ct)
    {
        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;

        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        if (!isManager)
        {
            var isMember = await _members.IsMemberAsync(project.Id, _currentUser.UserId.Value, ct);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        // Default to the current user when no assignee is specified.
        // Managers may explicitly assign to others; everyone else always gets self-assigned.
        var assignedUserId = isManager
            ? (request.AssignedUserId ?? _currentUser.UserId)
            : _currentUser.UserId;

        // Validate org membership and auto-add to project if needed.
        // This keeps ProjectMembers as the source of truth for "My Data" project lists.
        if (assignedUserId.HasValue)
        {
            if (assignedUserId.Value == _currentUser.UserId.Value)
            {
                var isMember = await _members.IsMemberAsync(project.Id, assignedUserId.Value, ct);
                if (!isMember)
                {
                    var member = ProjectMember.Create(project.OrgId, project.Id, assignedUserId.Value, _currentUser.UserId.Value);
                    await _members.AddAsync(member, ct);
                }
            }
            else
            {
                var assignee = await _users.GetByIdAsync(assignedUserId.Value, ct)
                    ?? throw new NotFoundException("User", assignedUserId.Value);
                if (assignee.OrgId != project.OrgId)
                    throw new ValidationException("AssignedUserId", "Assignee must belong to the same organization");
                var isMember = await _members.IsMemberAsync(project.Id, assignee.Id, ct);
                if (!isMember)
                {
                    var member = ProjectMember.Create(project.OrgId, project.Id, assignee.Id, _currentUser.UserId.Value);
                    await _members.AddAsync(member, ct);
                }
            }
        }

        var priority = ParsePriority(request.Priority);
        var maxPos = await _tasks.GetMaxPositionInColumnAsync(project.Id, ProjectTaskStatus.Todo, ct);

        var task = ProjectTask.Create(
            project.OrgId,
            project.Id,
            request.Title,
            _currentUser.UserId.Value,
            assignedUserId,
            request.Description,
            priority,
            request.DueDate,
            maxPos + 1024);

        await _tasks.AddAsync(task, ct);

        if (request.AssignedUserId.HasValue)
            await _notifications.NotifyTaskAssignedAsync(request.AssignedUserId.Value, task, project, ct);

        return TaskMapper.Map(task, project, null, null);
    }

    private static TaskPriority ParsePriority(string value) => value?.ToLowerInvariant() switch
    {
        "none" => TaskPriority.None,
        "low" => TaskPriority.Low,
        "high" => TaskPriority.High,
        "urgent" => TaskPriority.Urgent,
        _ => TaskPriority.Medium
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// UPDATE TASK
// ═══════════════════════════════════════════════════════════════════════════

public sealed record UpdateTaskCommand(
    Guid TaskId,
    string Title,
    string? Description,
    Guid? AssignedUserId,
    string Priority,
    DateTime? DueDate) : IRequest<TaskResponse>;

public sealed class UpdateTaskCommandHandler : IRequestHandler<UpdateTaskCommand, TaskResponse>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IUserRepository _users;
    private readonly ICurrentUserContext _currentUser;
    private readonly INotificationDispatcher _notifications;

    public UpdateTaskCommandHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        IProjectMemberRepository members,
        IUserRepository users,
        ICurrentUserContext currentUser,
        INotificationDispatcher notifications)
    {
        _tasks = tasks;
        _projects = projects;
        _members = members;
        _users = users;
        _currentUser = currentUser;
        _notifications = notifications;
    }

    public async Task<TaskResponse> Handle(UpdateTaskCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;

        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException("Task", request.TaskId);

        // Creators can edit their own tasks; managers can edit any task.
        if (!isManager && task.CreatedByUserId != _currentUser.UserId.Value)
            throw new ForbiddenException("You can only edit tasks you created");

        var project = task.Project ?? await _projects.GetByIdAsync(task.ProjectId, ct);
        if (project is null)
            throw new NotFoundException("Project", task.ProjectId);

        if (!isManager)
        {
            var isMember = await _members.IsMemberAsync(project.Id, _currentUser.UserId.Value, ct);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        var previousAssignee = task.AssignedUserId;

        if (request.AssignedUserId.HasValue && request.AssignedUserId != previousAssignee)
        {
            var assignee = await _users.GetByIdAsync(request.AssignedUserId.Value, ct)
                ?? throw new NotFoundException("User", request.AssignedUserId.Value);
            if (assignee.OrgId != project.OrgId)
                throw new ValidationException("AssignedUserId", "Assignee must belong to the same organization");
            var isMember = await _members.IsMemberAsync(project.Id, assignee.Id, ct);
            if (!isMember)
            {
                var member = ProjectMember.Create(project.OrgId, project.Id, assignee.Id, _currentUser.UserId.Value);
                await _members.AddAsync(member, ct);
            }
        }

        task.UpdateDetails(request.Title, request.Description, ParsePriority(request.Priority), request.DueDate, request.AssignedUserId);
        await _tasks.UpdateAsync(task, ct);

        // Notify newly assigned user
        if (request.AssignedUserId.HasValue && request.AssignedUserId != previousAssignee)
            await _notifications.NotifyTaskAssignedAsync(request.AssignedUserId.Value, task, project, ct);

        return TaskMapper.Map(task, project, null, null);
    }

    private static TaskPriority ParsePriority(string value) => value?.ToLowerInvariant() switch
    {
        "none" => TaskPriority.None,
        "low" => TaskPriority.Low,
        "high" => TaskPriority.High,
        "urgent" => TaskPriority.Urgent,
        _ => TaskPriority.Medium
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// MOVE TASK — the critical handler that drives the kanban timer
// ═══════════════════════════════════════════════════════════════════════════

public sealed record MoveTaskCommand(
    Guid TaskId,
    string Status,
    double? Position,
    uint? RowVersion) : IRequest<TaskResponse>;

public sealed class MoveTaskCommandHandler : IRequestHandler<MoveTaskCommand, TaskResponse>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly IProjectMemberRepository _members;
    private readonly IUserIntegrationRepository _integrations;
    private readonly ILinearClient _linear;
    private readonly IUserIntegrationTokenProtector _protector;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<MoveTaskCommandHandler> _logger;

    public MoveTaskCommandHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        IProjectMemberRepository members,
        IUserIntegrationRepository integrations,
        ILinearClient linear,
        IUserIntegrationTokenProtector protector,
        ICurrentUserContext currentUser,
        ILogger<MoveTaskCommandHandler> logger)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
        _members = members;
        _integrations = integrations;
        _linear = linear;
        _protector = protector;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TaskResponse> Handle(MoveTaskCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException();

        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException("Task", request.TaskId);

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;
        if (!isManager)
        {
            var isMember = await _members.IsMemberAsync(task.ProjectId, _currentUser.UserId.Value, ct);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        if (request.RowVersion.HasValue && request.RowVersion.Value != task.RowVersion)
            throw new ConflictException("task_version_stale", "Task has been modified by someone else. Reload and retry.");

        var newStatus = ParseStatus(request.Status);
        var oldStatus = task.Status;
        var now = DateTime.UtcNow;

        // InReview is only allowed on Linear-synced projects.
        if (newStatus == ProjectTaskStatus.InReview)
        {
            var projectForStatusCheck = task.Project ?? await _projects.GetByIdAsync(task.ProjectId, ct);
            if (projectForStatusCheck is null || projectForStatusCheck.SyncSource != ProjectSyncSource.Linear)
                throw new ValidationException("Status", "'InReview' column is only available on Linear-synced projects");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Timer side-effects
        // ─────────────────────────────────────────────────────────────────────

        // The timer always tracks the user who is moving the card, not the assignee.
        var workerId = _currentUser.UserId.Value;

        if (oldStatus == ProjectTaskStatus.InProgress && newStatus != ProjectTaskStatus.InProgress)
        {
            // Closing an in-progress phase: we expect at most one open entry per user,
            // but historically duplicates could exist. Close ALL open entries to ensure
            // we always credit time correctly and end up in a consistent state.
            await CloseAllOpenEntriesForUserAsync(workerId, now, preferredKeepOpenTaskId: null, ct);
        }

        if (newStatus == ProjectTaskStatus.InProgress && oldStatus != ProjectTaskStatus.InProgress)
        {
            // Starting a new in-progress phase: close any existing open entries for this user
            // so we guarantee at most one open timer (and don't leak time to the wrong task).
            await CloseAllOpenEntriesForUserAsync(workerId, now, preferredKeepOpenTaskId: null, ct);

            var entry = TaskTimeEntry.Open(task.OrgId, task.Id, workerId);
            await _entries.AddAsync(entry, ct);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Apply the move + persist
        // ─────────────────────────────────────────────────────────────────────

        var newPos = request.Position ?? (await _tasks.GetMaxPositionInColumnAsync(task.ProjectId, newStatus, ct)) + 1024;
        task.MoveTo(newStatus, newPos);
        await _tasks.UpdateAsync(task, ct);

        // ─────────────────────────────────────────────────────────────────────
        // Best-effort push to Linear. Never fail the local move if Linear
        // can't be reached — the user's app must stay responsive.
        // ─────────────────────────────────────────────────────────────────────
        if (task.IsLinearSourced && oldStatus != newStatus)
        {
            try
            {
                await PushStatusToLinearAsync(task, newStatus, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Linear status push failed for task {TaskId}; local move stands", task.Id);
            }
        }

        var project = task.Project ?? await _projects.GetByIdAsync(task.ProjectId, ct);
        return TaskMapper.Map(task, project, null, null);
    }

    /// <summary>
    /// Closes ALL open task time entries for a user, credits time to their tasks,
    /// and normalizes any affected tasks back to Todo (except the task we are
    /// about to start, if provided).
    /// </summary>
    private async Task CloseAllOpenEntriesForUserAsync(
        Guid workerId,
        DateTime nowUtc,
        Guid? preferredKeepOpenTaskId,
        CancellationToken ct)
    {
        var openEntries = await _entries.ListOpenForUserAsync(workerId, ct);
        if (openEntries.Count == 0) return;

        foreach (var open in openEntries)
        {
            // If we ever introduce "keep one open" flows, this hook allows it.
            if (preferredKeepOpenTaskId.HasValue && open.TaskId == preferredKeepOpenTaskId.Value)
                continue;

            var added = open.Close(nowUtc);
            await _entries.UpdateAsync(open, ct);

            // Credit the task if it still exists.
            var affectedTask = await _tasks.GetByIdAsync(open.TaskId, ct);
            if (affectedTask is null) continue;

            if (added > 0) affectedTask.AccumulateWorkedTime(added);

            // If the task still claims to be InProgress but its timer is closed,
            // flip it back to Todo so the UI doesn't show a "running" status.
            if (affectedTask.Status == ProjectTaskStatus.InProgress &&
                (!preferredKeepOpenTaskId.HasValue || affectedTask.Id != preferredKeepOpenTaskId.Value))
            {
                affectedTask.MoveTo(ProjectTaskStatus.Todo, affectedTask.Position);
            }

            await _tasks.UpdateAsync(affectedTask, ct);
        }
    }

    private async Task PushStatusToLinearAsync(ProjectTask task, ProjectTaskStatus newStatus, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) return;
        if (string.IsNullOrWhiteSpace(task.LinearIssueId) || string.IsNullOrWhiteSpace(task.LinearTeamId))
            return;

        var integration = await _integrations.GetAsync(_currentUser.UserId.Value, UserIntegrationProvider.Linear, ct);
        if (integration is null || integration.Status != UserIntegrationStatus.Active)
            return;

        string apiKey;
        try
        {
            apiKey = _protector.Unprotect(integration.EncryptedToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt Linear token on status push");
            return;
        }

        // Try the cache first — team workflow states rarely change, so an hour of
        // staleness is fine and it skips the GraphQL round-trip on every move.
        var now = DateTime.UtcNow;
        IReadOnlyList<LinearWorkflowState>? teamStates =
            LinearTeamStateCache.TryGet(integration, task.LinearTeamId!, now);

        if (teamStates is null)
        {
            try
            {
                teamStates = await _linear.GetTeamStatesAsync(apiKey, task.LinearTeamId!, ct);
                LinearTeamStateCache.Store(integration, task.LinearTeamId!, teamStates, now);
                // Integration write happens below in the success/failure branches.
            }
            catch (LinearUnauthorizedException ex)
            {
                integration.MarkUnauthorized(ex.Message);
                LinearPendingPushQueue.Enqueue(integration, task.Id, newStatus, "unauthorized");
                await _integrations.UpdateAsync(integration, ct);
                return;
            }
            catch (LinearApiException ex)
            {
                LinearPendingPushQueue.Enqueue(integration, task.Id, newStatus, ex.Message);
                await _integrations.UpdateAsync(integration, ct);
                return;
            }
        }

        var target = LinearStateMapper.ResolveLinearState(newStatus, teamStates);
        if (target is null)
        {
            _logger.LogWarning("No Linear state resolved for {Status} on team {TeamId}", newStatus, task.LinearTeamId);
            LinearPendingPushQueue.Enqueue(integration, task.Id, newStatus, "no_linear_state_match");
            await _integrations.UpdateAsync(integration, ct);
            return;
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
                LinearPendingPushQueue.Enqueue(integration, task.Id, newStatus, "issue_update_returned_false");
            }
            integration.MarkUsed();
            await _integrations.UpdateAsync(integration, ct);
        }
        catch (LinearUnauthorizedException ex)
        {
            integration.MarkUnauthorized(ex.Message);
            LinearPendingPushQueue.Enqueue(integration, task.Id, newStatus, "unauthorized");
            await _integrations.UpdateAsync(integration, ct);
        }
        catch (LinearApiException ex)
        {
            _logger.LogWarning(ex, "Linear status push API failure for task {TaskId}", task.Id);
            LinearPendingPushQueue.Enqueue(integration, task.Id, newStatus, ex.Message);
            await _integrations.UpdateAsync(integration, ct);
        }
    }

    private static ProjectTaskStatus ParseStatus(string value) => value?.ToLowerInvariant() switch
    {
        "todo" => ProjectTaskStatus.Todo,
        "inprogress" or "in_progress" => ProjectTaskStatus.InProgress,
        "inreview" or "in_review" or "review" => ProjectTaskStatus.InReview,
        "done" => ProjectTaskStatus.Done,
        _ => throw new ValidationException("Status", $"Unknown status '{value}'")
    };
}

// ═══════════════════════════════════════════════════════════════════════════
// DELETE TASK (soft delete)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record DeleteTaskCommand(Guid TaskId) : IRequest<Unit>;

public sealed class DeleteTaskCommandHandler : IRequestHandler<DeleteTaskCommand, Unit>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly IProjectMemberRepository _members;
    private readonly ICurrentUserContext _currentUser;

    public DeleteTaskCommandHandler(
        IProjectTaskRepository tasks,
        ITaskTimeEntryRepository entries,
        IProjectMemberRepository members,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _entries = entries;
        _members = members;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;

        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException("Task", request.TaskId);

        if (!isManager && task.CreatedByUserId != _currentUser.UserId.Value)
            throw new ForbiddenException("You can only delete tasks you created");

        if (!isManager)
        {
            var isMember = await _members.IsMemberAsync(task.ProjectId, _currentUser.UserId.Value, ct);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        // Close any in-progress entry tied to this task.
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
        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PAUSE / RESUME / REJECT (idle handling)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record PauseOpenTaskTimerCommand : IRequest<Unit>;

public sealed class PauseOpenTaskTimerCommandHandler : IRequestHandler<PauseOpenTaskTimerCommand, Unit>
{
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public PauseOpenTaskTimerCommandHandler(ITaskTimeEntryRepository entries, ICurrentUserContext currentUser)
    {
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(PauseOpenTaskTimerCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();
        var openEntries = await _entries.ListOpenForUserAsync(_currentUser.UserId.Value, ct);
        foreach (var open in openEntries)
        {
            open.Pause();
            await _entries.UpdateAsync(open, ct);
        }
        return Unit.Value;
    }
}

public sealed record ResumeOpenTaskTimerCommand : IRequest<Unit>;

public sealed class ResumeOpenTaskTimerCommandHandler : IRequestHandler<ResumeOpenTaskTimerCommand, Unit>
{
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public ResumeOpenTaskTimerCommandHandler(ITaskTimeEntryRepository entries, ICurrentUserContext currentUser)
    {
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(ResumeOpenTaskTimerCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();
        var openEntries = await _entries.ListOpenForUserAsync(_currentUser.UserId.Value, ct);
        foreach (var open in openEntries)
        {
            open.Resume();
            await _entries.UpdateAsync(open, ct);
        }
        return Unit.Value;
    }
}

public sealed record CloseOpenTaskTimerOnIdleRejectCommand : IRequest<Unit>;

public sealed class CloseOpenTaskTimerOnIdleRejectCommandHandler : IRequestHandler<CloseOpenTaskTimerOnIdleRejectCommand, Unit>
{
    private readonly ITaskTimeEntryRepository _entries;
    private readonly IProjectTaskRepository _tasks;
    private readonly ICurrentUserContext _currentUser;

    public CloseOpenTaskTimerOnIdleRejectCommandHandler(
        ITaskTimeEntryRepository entries,
        IProjectTaskRepository tasks,
        ICurrentUserContext currentUser)
    {
        _entries = entries;
        _tasks = tasks;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(CloseOpenTaskTimerOnIdleRejectCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();
        var openEntries = await _entries.ListOpenForUserAsync(_currentUser.UserId.Value, ct);
        foreach (var open in openEntries)
        {
            var added = open.Close();
            await _entries.UpdateAsync(open, ct);

            var task = await _tasks.GetByIdAsync(open.TaskId, ct);
            if (task is null) continue;

            if (added > 0) task.AccumulateWorkedTime(added);
            // Move it back to Todo so the user can resume later.
            if (task.Status == ProjectTaskStatus.InProgress)
                task.MoveTo(ProjectTaskStatus.Todo, task.Position);
            await _tasks.UpdateAsync(task, ct);
        }
        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MAPPER
// ═══════════════════════════════════════════════════════════════════════════

internal static class TaskMapper
{
    public static TaskResponse Map(ProjectTask task, Project? project, TaskTimeEntry? openEntry, string? assignedUserDisplayName)
    {
        var now = DateTime.UtcNow;
        long? running = null;
        if (openEntry is not null && openEntry.IsOpen)
            running = (long)(now - openEntry.StartedAt).TotalSeconds - openEntry.PausedSeconds;

        return new TaskResponse
        {
            Id = task.Id,
            ProjectId = task.ProjectId,
            ProjectName = project?.Name ?? string.Empty,
            ProjectColor = project?.Color ?? "#4A9FFF",
            Title = task.Title,
            Description = task.Description,
            Status = task.Status.ToString(),
            CreatedByUserId = task.CreatedByUserId,
            AssignedUserId = task.AssignedUserId,
            AssignedUserDisplayName = assignedUserDisplayName ?? task.AssignedUser?.DisplayName,
            Priority = task.Priority.ToString(),
            DueDate = task.DueDate,
            Position = task.Position,
            CreatedAt = task.CreatedAt,
            UpdatedAt = task.UpdatedAt,
            MovedToInProgressAt = task.MovedToInProgressAt,
            CompletedAt = task.CompletedAt,
            TotalSecondsWorked = task.TotalSecondsWorked,
            RowVersion = task.RowVersion,
            IsRunning = openEntry is not null && openEntry.IsOpen,
            RunningSeconds = running,
            IsLinearSourced = task.IsLinearSourced,
            LinearIssueIdentifier = task.LinearIssueIdentifier,
            LinearUrl = task.LinearUrl,
            LinearStateName = task.LinearStateName
        };
    }
}
