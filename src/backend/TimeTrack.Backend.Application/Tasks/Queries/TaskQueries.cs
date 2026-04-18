using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Tasks.Commands;
using TimeTrack.Backend.Application.Tasks.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Tasks.Queries;

// ═══════════════════════════════════════════════════════════════════════════
// LIST TASKS FOR A PROJECT (kanban board)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListProjectTasksQuery(Guid ProjectId) : IRequest<ListTasksResponse>;

public sealed class ListProjectTasksQueryHandler : IRequestHandler<ListProjectTasksQuery, ListTasksResponse>
{
    private readonly IProjectRepository _projects;
    private readonly IProjectMemberRepository _members;
    private readonly IProjectTaskRepository _tasks;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public ListProjectTasksQueryHandler(
        IProjectRepository projects,
        IProjectMemberRepository members,
        IProjectTaskRepository tasks,
        ITaskTimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _projects = projects;
        _members = members;
        _tasks = tasks;
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<ListTasksResponse> Handle(ListProjectTasksQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;
        if (!isManager)
        {
            var isMember = await _members.IsMemberAsync(project.Id, _currentUser.UserId.Value, ct);
            if (!isMember)
                throw new ForbiddenException("You are not a member of this project");
        }

        var tasks = await _tasks.ListByProjectAsync(project.Id, ct);

        // Find which tasks have an open timer right now (one DB query per assignee that has tasks)
        var openByTaskId = new Dictionary<Guid, Domain.Entities.TaskTimeEntry>();
        var assigneeIds = tasks.Select(t => t.AssignedUserId).Where(u => u.HasValue).Select(u => u!.Value).Distinct();
        foreach (var assignee in assigneeIds)
        {
            var open = await _entries.GetOpenForUserAsync(assignee, ct);
            if (open is not null) openByTaskId[open.TaskId] = open;
        }

        var responses = tasks.Select(t =>
        {
            openByTaskId.TryGetValue(t.Id, out var openEntry);
            return InternalMap(t, project.Name, project.Color, openEntry);
        }).ToList();

        return new ListTasksResponse
        {
            Tasks = responses,
            TodoCount = responses.Count(r => r.Status == "Todo"),
            InProgressCount = responses.Count(r => r.Status == "InProgress"),
            DoneCount = responses.Count(r => r.Status == "Done"),
            TotalSecondsWorked = responses.Sum(r => r.TotalSecondsWorked)
        };
    }

    internal static TaskResponse InternalMap(Domain.Entities.ProjectTask t, string projectName, string projectColor, Domain.Entities.TaskTimeEntry? openEntry)
    {
        var now = DateTime.UtcNow;
        long? running = null;
        if (openEntry is not null && openEntry.IsOpen)
        {
            var effectiveNow = openEntry.IsPaused && openEntry.PausedAt.HasValue
                ? openEntry.PausedAt.Value
                : now;
            running = (long)(effectiveNow - openEntry.StartedAt).TotalSeconds - openEntry.PausedSeconds;
        }

        return new TaskResponse
        {
            Id = t.Id,
            ProjectId = t.ProjectId,
            ProjectName = projectName,
            ProjectColor = projectColor,
            Title = t.Title,
            Description = t.Description,
            Status = t.Status.ToString(),
            CreatedByUserId = t.CreatedByUserId,
            AssignedUserId = t.AssignedUserId,
            AssignedUserDisplayName = t.AssignedUser?.DisplayName,
            Priority = t.Priority.ToString(),
            DueDate = t.DueDate,
            Position = t.Position,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            MovedToInProgressAt = t.MovedToInProgressAt,
            CompletedAt = t.CompletedAt,
            TotalSecondsWorked = t.TotalSecondsWorked,
            RowVersion = t.RowVersion,
            IsRunning = openEntry is not null && openEntry.IsOpen && !openEntry.IsPaused,
            IsPaused = openEntry is not null && openEntry.IsOpen && openEntry.IsPaused,
            RunningSeconds = running,
            IsLinearSourced = t.IsLinearSourced,
            LinearIssueIdentifier = t.LinearIssueIdentifier,
            LinearUrl = t.LinearUrl,
            LinearStateName = t.LinearStateName
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// LIST TASKS FOR A SPECIFIC USER (admin/manager reports)
// ═══════════════════════════════════════════════════════════════════════════

/// <summary>
/// Query to list tasks for a specific user (or all org users).
/// Only accessible by Admin/Gestor roles for users within their organization.
/// </summary>
public sealed record ListUserTasksQuery(Guid? TargetUserId, bool IncludeDone = false) : IRequest<ListTasksResponse>;

public sealed class ListUserTasksQueryHandler : IRequestHandler<ListUserTasksQuery, ListTasksResponse>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly IUserRepository _users;
    private readonly ICurrentUserContext _currentUser;

    public ListUserTasksQueryHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        IUserRepository users,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task<ListTasksResponse> Handle(ListUserTasksQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException();

        var orgId = _currentUser.OrgId.Value;

        // Fetch tasks: either for a specific user or for the whole org
        List<Domain.Entities.ProjectTask> tasks;
        if (request.TargetUserId.HasValue)
        {
            // Verify target user belongs to the same org
            var targetUser = await _users.GetByIdAsync(request.TargetUserId.Value, ct);
            if (targetUser == null || targetUser.OrgId != orgId)
                throw new NotFoundException("User", request.TargetUserId.Value);

            tasks = (List<Domain.Entities.ProjectTask>)await _tasks.ListAssignedToUserAsync(
                request.TargetUserId.Value, request.IncludeDone, ct);
        }
        else
        {
            // All team members — fetch by org
            tasks = (List<Domain.Entities.ProjectTask>)await _tasks.ListByOrgAsync(
                orgId, request.IncludeDone, ct);
        }

        // Build project cache
        var projectCache = new Dictionary<Guid, Domain.Entities.Project>();
        foreach (var t in tasks)
        {
            if (t.Project is not null) projectCache[t.ProjectId] = t.Project;
            else if (!projectCache.ContainsKey(t.ProjectId))
            {
                var p = await _projects.GetByIdAsync(t.ProjectId, ct);
                if (p is not null) projectCache[t.ProjectId] = p;
            }
        }

        // Find open entries for relevant users
        var assigneeIds = tasks
            .Select(t => t.AssignedUserId)
            .Where(u => u.HasValue)
            .Select(u => u!.Value)
            .Distinct();

        var openByTaskId = new Dictionary<Guid, Domain.Entities.TaskTimeEntry>();
        foreach (var assignee in assigneeIds)
        {
            var open = await _entries.GetOpenForUserAsync(assignee, ct);
            if (open is not null) openByTaskId[open.TaskId] = open;
        }

        var responses = tasks.Select(t =>
        {
            projectCache.TryGetValue(t.ProjectId, out var project);
            openByTaskId.TryGetValue(t.Id, out var openEntry);
            return ListProjectTasksQueryHandler.InternalMap(t, project?.Name ?? string.Empty, project?.Color ?? "#4A9FFF", openEntry);
        }).ToList();

        return new ListTasksResponse
        {
            Tasks = responses,
            TodoCount = responses.Count(r => r.Status == "Todo"),
            InProgressCount = responses.Count(r => r.Status == "InProgress"),
            DoneCount = responses.Count(r => r.Status == "Done"),
            TotalSecondsWorked = responses.Sum(r => r.TotalSecondsWorked)
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// LIST MY TASKS (for desktop agent)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListMyTasksQuery(bool IncludeDone = false) : IRequest<ListTasksResponse>;

public sealed class ListMyTasksQueryHandler : IRequestHandler<ListMyTasksQuery, ListTasksResponse>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly IProjectMemberRepository _members;
    private readonly ICurrentUserContext _currentUser;

    public ListMyTasksQueryHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        IProjectMemberRepository members,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
        _members = members;
        _currentUser = currentUser;
    }

    public async Task<ListTasksResponse> Handle(ListMyTasksQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue) throw new UnauthorizedAccessException();

        var userId = _currentUser.UserId.Value;
        var orgId = _currentUser.OrgId.Value;

        var tasks = await _tasks.ListAssignedToUserAsync(userId, request.IncludeDone, ct);
        var openEntry = await _entries.GetOpenForUserAsync(userId, ct);

        // Self-heal: tasks assigned to a user imply project membership.
        var taskProjectIds = tasks.Select(t => t.ProjectId).Distinct().ToList();
        if (taskProjectIds.Count > 0)
        {
            var existing = await _members.ListProjectIdsForUserAsync(userId, ct);
            var set = existing.Count > 0 ? existing.ToHashSet() : new HashSet<Guid>();
            foreach (var pid in taskProjectIds)
            {
                if (set.Contains(pid)) continue;
                var member = Domain.Entities.ProjectMember.Create(orgId, pid, userId, userId);
                await _members.AddAsync(member, ct);
                set.Add(pid);
            }
        }

        var projectCache = new Dictionary<Guid, Domain.Entities.Project>();
        foreach (var t in tasks)
        {
            if (t.Project is not null) projectCache[t.ProjectId] = t.Project;
            else if (!projectCache.ContainsKey(t.ProjectId))
            {
                var p = await _projects.GetByIdAsync(t.ProjectId, ct);
                if (p is not null) projectCache[t.ProjectId] = p;
            }
        }

        var responses = tasks.Select(t =>
        {
            projectCache.TryGetValue(t.ProjectId, out var project);
            var entry = (openEntry is not null && openEntry.TaskId == t.Id) ? openEntry : null;
            return ListProjectTasksQueryHandler.InternalMap(t, project?.Name ?? string.Empty, project?.Color ?? "#4A9FFF", entry);
        }).ToList();

        return new ListTasksResponse
        {
            Tasks = responses,
            TodoCount = responses.Count(r => r.Status == "Todo"),
            InProgressCount = responses.Count(r => r.Status == "InProgress"),
            DoneCount = responses.Count(r => r.Status == "Done"),
            TotalSecondsWorked = responses.Sum(r => r.TotalSecondsWorked)
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GET TASK BY ID (for the detail drawer)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record GetTaskByIdQuery(Guid TaskId) : IRequest<TaskResponse>;

public sealed class GetTaskByIdQueryHandler : IRequestHandler<GetTaskByIdQuery, TaskResponse>
{
    private readonly IProjectTaskRepository _tasks;
    private readonly IProjectRepository _projects;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly IProjectMemberRepository _members;
    private readonly ICurrentUserContext _currentUser;

    public GetTaskByIdQueryHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        IProjectMemberRepository members,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
        _members = members;
        _currentUser = currentUser;
    }

    public async Task<TaskResponse> Handle(GetTaskByIdQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

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

        var project = task.Project ?? await _projects.GetByIdAsync(task.ProjectId, ct);

        Domain.Entities.TaskTimeEntry? openEntry = null;
        if (task.AssignedUserId.HasValue)
        {
            var open = await _entries.GetOpenForUserAsync(task.AssignedUserId.Value, ct);
            if (open is not null && open.TaskId == task.Id)
                openEntry = open;
        }

        return ListProjectTasksQueryHandler.InternalMap(task, project?.Name ?? string.Empty, project?.Color ?? "#4A9FFF", openEntry);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// GET MY OPEN TASK (for floating bar polling)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record GetMyOpenTaskQuery : IRequest<OpenTaskResponse?>;

public sealed class GetMyOpenTaskQueryHandler : IRequestHandler<GetMyOpenTaskQuery, OpenTaskResponse?>
{
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public GetMyOpenTaskQueryHandler(ITaskTimeEntryRepository entries, ICurrentUserContext currentUser)
    {
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<OpenTaskResponse?> Handle(GetMyOpenTaskQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();
        var open = await _entries.GetOpenForUserAsync(_currentUser.UserId.Value, ct);
        if (open is null || open.Task is null) return null;

        var elapsed = (long)(DateTime.UtcNow - open.StartedAt).TotalSeconds - open.PausedSeconds;
        if (open.IsPaused && open.PausedAt.HasValue)
            elapsed = (long)(open.PausedAt.Value - open.StartedAt).TotalSeconds - open.PausedSeconds;

        return new OpenTaskResponse
        {
            TaskId = open.TaskId,
            ProjectId = open.Task.ProjectId,
            ProjectName = open.Task.Project?.Name ?? string.Empty,
            ProjectColor = open.Task.Project?.Color ?? "#4A9FFF",
            TaskTitle = open.Task.Title,
            StartedAt = open.StartedAt,
            PausedSeconds = open.PausedSeconds,
            IsPaused = open.IsPaused,
            ElapsedSeconds = Math.Max(0, elapsed)
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// LIST MY TASK ENTRIES FOR A DATE (for the activity timeline)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListMyTaskEntriesQuery(DateOnly Date) : IRequest<ListTaskEntriesResponse>;

public sealed class ListMyTaskEntriesQueryHandler : IRequestHandler<ListMyTaskEntriesQuery, ListTaskEntriesResponse>
{
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public ListMyTaskEntriesQueryHandler(ITaskTimeEntryRepository entries, ICurrentUserContext currentUser)
    {
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<ListTaskEntriesResponse> Handle(ListMyTaskEntriesQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException();

        var entries = await _entries.ListForUserOnDateAsync(_currentUser.UserId.Value, request.Date, ct);

        var dtos = entries.Select(e => new TaskEntryDto
        {
            Id = e.Id,
            TaskTitle = e.Task?.Title ?? string.Empty,
            ProjectName = e.Task?.Project?.Name ?? string.Empty,
            ProjectColor = e.Task?.Project?.Color ?? "#4A9FFF",
            StartedAt = e.StartedAt,
            EndedAt = e.EndedAt,
            PausedAt = e.PausedAt,
            IsPaused = e.IsPaused
        }).ToList();

        return new ListTaskEntriesResponse { Entries = dtos };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MANAGER: LIST TASK ENTRIES FOR A USER ON A DATE (for team activity views)
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListUserTaskEntriesQuery(Guid UserId, DateOnly Date) : IRequest<ListTaskEntriesResponse>;

public sealed class ListUserTaskEntriesQueryHandler : IRequestHandler<ListUserTaskEntriesQuery, ListTaskEntriesResponse>
{
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public ListUserTaskEntriesQueryHandler(ITaskTimeEntryRepository entries, ICurrentUserContext currentUser)
    {
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<ListTaskEntriesResponse> Handle(ListUserTaskEntriesQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue || !_currentUser.OrgId.HasValue)
            throw new UnauthorizedAccessException();

        var role = _currentUser.Role;
        var isManager = role == UserRole.Admin || role == UserRole.Gestor;
        if (!isManager)
            throw new ForbiddenException("You are not allowed to view other users' task entries");

        var entries = await _entries.ListForUserOnDateAsync(request.UserId, request.Date, ct);

        var dtos = entries.Select(e => new TaskEntryDto
        {
            Id = e.Id,
            TaskTitle = e.Task?.Title ?? string.Empty,
            ProjectName = e.Task?.Project?.Name ?? string.Empty,
            ProjectColor = e.Task?.Project?.Color ?? "#4A9FFF",
            StartedAt = e.StartedAt,
            EndedAt = e.EndedAt,
            PausedAt = e.PausedAt,
            IsPaused = e.IsPaused
        }).ToList();

        return new ListTaskEntriesResponse { Entries = dtos };
    }
}
