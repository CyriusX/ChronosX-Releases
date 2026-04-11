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
        if (role != UserRole.Admin && role != UserRole.Gestor)
            throw new ForbiddenException("Only managers can create tasks");

        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        if (request.AssignedUserId.HasValue)
        {
            var assignee = await _users.GetByIdAsync(request.AssignedUserId.Value, ct)
                ?? throw new NotFoundException("User", request.AssignedUserId.Value);
            if (assignee.OrgId != project.OrgId)
                throw new ValidationException("AssignedUserId", "Assignee must belong to the same organization");
            var isMember = await _members.IsMemberAsync(project.Id, assignee.Id, ct);
            if (!isMember)
                throw new ValidationException("AssignedUserId", "Assignee must be a project member");
        }

        var priority = ParsePriority(request.Priority);
        var maxPos = await _tasks.GetMaxPositionInColumnAsync(project.Id, ProjectTaskStatus.Todo, ct);

        var task = ProjectTask.Create(
            project.OrgId,
            project.Id,
            request.Title,
            _currentUser.UserId.Value,
            request.AssignedUserId,
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
        var role = _currentUser.Role;
        if (role != UserRole.Admin && role != UserRole.Gestor)
            throw new ForbiddenException("Only managers can edit tasks");

        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException("Task", request.TaskId);

        var project = task.Project ?? await _projects.GetByIdAsync(task.ProjectId, ct);
        if (project is null)
            throw new NotFoundException("Project", task.ProjectId);

        var previousAssignee = task.AssignedUserId;

        if (request.AssignedUserId.HasValue && request.AssignedUserId != previousAssignee)
        {
            var assignee = await _users.GetByIdAsync(request.AssignedUserId.Value, ct)
                ?? throw new NotFoundException("User", request.AssignedUserId.Value);
            if (assignee.OrgId != project.OrgId)
                throw new ValidationException("AssignedUserId", "Assignee must belong to the same organization");
            var isMember = await _members.IsMemberAsync(project.Id, assignee.Id, ct);
            if (!isMember)
                throw new ValidationException("AssignedUserId", "Assignee must be a project member");
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
    private readonly IUserIntegrationRepository _integrations;
    private readonly ILinearClient _linear;
    private readonly IUserIntegrationTokenProtector _protector;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<MoveTaskCommandHandler> _logger;

    public MoveTaskCommandHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        IUserIntegrationRepository integrations,
        ILinearClient linear,
        IUserIntegrationTokenProtector protector,
        ICurrentUserContext currentUser,
        ILogger<MoveTaskCommandHandler> logger)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
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
        var isAssignee = task.AssignedUserId == _currentUser.UserId;

        if (!isManager && !isAssignee)
            throw new ForbiddenException("Only the assignee or a manager can move this task");

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

        if (oldStatus == ProjectTaskStatus.InProgress && newStatus != ProjectTaskStatus.InProgress)
        {
            // Closing the in-progress phase: close any open entry on this task for this user.
            var open = await _entries.GetOpenForUserAsync(task.AssignedUserId ?? _currentUser.UserId.Value, ct);
            if (open is not null && open.TaskId == task.Id)
            {
                var added = open.Close(now);
                await _entries.UpdateAsync(open, ct);
                if (added > 0)
                    task.AccumulateWorkedTime(added);
            }
        }

        if (newStatus == ProjectTaskStatus.InProgress && oldStatus != ProjectTaskStatus.InProgress)
        {
            // Auto-stop any other open entry the user has (only ONE in-progress task at a time).
            var workerId = task.AssignedUserId ?? _currentUser.UserId.Value;
            var existingOpen = await _entries.GetOpenForUserAsync(workerId, ct);
            if (existingOpen is not null)
            {
                var added = existingOpen.Close(now);
                await _entries.UpdateAsync(existingOpen, ct);
                // Try to credit the previous task too.
                if (added > 0 && existingOpen.TaskId != task.Id)
                {
                    var prevTask = await _tasks.GetByIdAsync(existingOpen.TaskId, ct);
                    if (prevTask is not null)
                    {
                        prevTask.AccumulateWorkedTime(added);
                        // Auto-flip the previous task back to Todo so it doesn't stay in-progress.
                        prevTask.MoveTo(ProjectTaskStatus.Todo, prevTask.Position);
                        await _tasks.UpdateAsync(prevTask, ct);
                    }
                }
            }

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

        IReadOnlyList<LinearWorkflowState> teamStates;
        try
        {
            teamStates = await _linear.GetTeamStatesAsync(apiKey, task.LinearTeamId!, ct);
        }
        catch (LinearUnauthorizedException ex)
        {
            integration.MarkUnauthorized(ex.Message);
            await _integrations.UpdateAsync(integration, ct);
            return;
        }

        var target = LinearStateMapper.ResolveLinearState(newStatus, teamStates);
        if (target is null)
        {
            _logger.LogWarning("No Linear state resolved for {Status} on team {TeamId}", newStatus, task.LinearTeamId);
            return;
        }

        try
        {
            var ok = await _linear.UpdateIssueStateAsync(apiKey, task.LinearIssueId!, target.Id, ct);
            if (ok)
            {
                task.UpdateLinearState(target.Id, target.Name);
                await _tasks.UpdateAsync(task, ct);
            }
            integration.MarkUsed();
            await _integrations.UpdateAsync(integration, ct);
        }
        catch (LinearUnauthorizedException ex)
        {
            integration.MarkUnauthorized(ex.Message);
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
    private readonly ICurrentUserContext _currentUser;

    public DeleteTaskCommandHandler(
        IProjectTaskRepository tasks,
        ITaskTimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(DeleteTaskCommand request, CancellationToken ct)
    {
        var role = _currentUser.Role;
        if (role != UserRole.Admin && role != UserRole.Gestor)
            throw new ForbiddenException("Only managers can delete tasks");

        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException("Task", request.TaskId);

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
        var open = await _entries.GetOpenForUserAsync(_currentUser.UserId.Value, ct);
        if (open is null) return Unit.Value;
        open.Pause();
        await _entries.UpdateAsync(open, ct);
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
        var open = await _entries.GetOpenForUserAsync(_currentUser.UserId.Value, ct);
        if (open is null) return Unit.Value;
        open.Resume();
        await _entries.UpdateAsync(open, ct);
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
        var open = await _entries.GetOpenForUserAsync(_currentUser.UserId.Value, ct);
        if (open is null) return Unit.Value;

        var added = open.Close();
        await _entries.UpdateAsync(open, ct);

        var task = await _tasks.GetByIdAsync(open.TaskId, ct);
        if (task is not null)
        {
            if (added > 0) task.AccumulateWorkedTime(added);
            // Move it back to Todo so the user can resume later.
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
            RunningSeconds = running
        };
    }
}
