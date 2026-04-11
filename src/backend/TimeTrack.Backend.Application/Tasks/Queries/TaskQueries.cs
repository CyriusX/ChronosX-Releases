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
    private readonly IProjectTaskRepository _tasks;
    private readonly ITaskTimeEntryRepository _entries;
    private readonly ICurrentUserContext _currentUser;

    public ListProjectTasksQueryHandler(
        IProjectRepository projects,
        IProjectTaskRepository tasks,
        ITaskTimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _projects = projects;
        _tasks = tasks;
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<ListTasksResponse> Handle(ListProjectTasksQuery request, CancellationToken ct)
    {
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NotFoundException("Project", request.ProjectId);

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
            running = (long)(now - openEntry.StartedAt).TotalSeconds - openEntry.PausedSeconds;

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
            IsRunning = openEntry is not null && openEntry.IsOpen,
            RunningSeconds = running,
            IsLinearSourced = t.IsLinearSourced,
            LinearIssueIdentifier = t.LinearIssueIdentifier,
            LinearUrl = t.LinearUrl,
            LinearStateName = t.LinearStateName
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
    private readonly ICurrentUserContext _currentUser;

    public ListMyTasksQueryHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<ListTasksResponse> Handle(ListMyTasksQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var tasks = await _tasks.ListAssignedToUserAsync(_currentUser.UserId.Value, request.IncludeDone, ct);
        var openEntry = await _entries.GetOpenForUserAsync(_currentUser.UserId.Value, ct);

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
    private readonly ICurrentUserContext _currentUser;

    public GetTaskByIdQueryHandler(
        IProjectTaskRepository tasks,
        IProjectRepository projects,
        ITaskTimeEntryRepository entries,
        ICurrentUserContext currentUser)
    {
        _tasks = tasks;
        _projects = projects;
        _entries = entries;
        _currentUser = currentUser;
    }

    public async Task<TaskResponse> Handle(GetTaskByIdQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var task = await _tasks.GetByIdAsync(request.TaskId, ct)
            ?? throw new NotFoundException("Task", request.TaskId);

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
            EndedAt = e.EndedAt
        }).ToList();

        return new ListTaskEntriesResponse { Entries = dtos };
    }
}
