using System.Text.Json;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Notifications;

public sealed class NotificationDispatcher : INotificationDispatcher
{
    private readonly IAgentNotificationInboxRepository _inbox;
    private readonly IDeviceRepository _devices;
    private readonly IRemoteCommandRepository _commands;
    private readonly IUserRepository _users;
    private readonly ICurrentUserContext _currentUser;

    public NotificationDispatcher(
        IAgentNotificationInboxRepository inbox,
        IDeviceRepository devices,
        IRemoteCommandRepository commands,
        IUserRepository users,
        ICurrentUserContext currentUser)
    {
        _inbox = inbox;
        _devices = devices;
        _commands = commands;
        _users = users;
        _currentUser = currentUser;
    }

    public async Task NotifyTaskAssignedAsync(Guid userId, ProjectTask task, Project project, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Serialize(new
        {
            taskId = task.Id,
            projectId = project.Id,
            projectName = project.Name,
            projectColor = project.Color,
            taskTitle = task.Title
        });

        var notification = AgentNotificationInbox.Create(
            project.OrgId,
            userId,
            AgentNotificationKind.TaskAssigned,
            "Nova tarefa atribuída",
            $"{project.Name} · {task.Title}",
            meta);

        await _inbox.AddAsync(notification, ct);
        await QueueAgentCommandAsync(userId, "task_assigned", meta, notification.Id, ct);
    }

    public async Task NotifyTaskUnassignedAsync(Guid userId, ProjectTask task, Project project, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Serialize(new
        {
            taskId = task.Id,
            projectId = project.Id,
            projectName = project.Name,
            taskTitle = task.Title
        });

        var notification = AgentNotificationInbox.Create(
            project.OrgId,
            userId,
            AgentNotificationKind.TaskUnassigned,
            "Tarefa removida",
            $"{project.Name} · {task.Title}",
            meta);

        await _inbox.AddAsync(notification, ct);
        await QueueAgentCommandAsync(userId, "task_unassigned", meta, notification.Id, ct);
    }

    public async Task NotifyDeadlineTodayAsync(Guid userId, ProjectTask task, Project project, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Serialize(new
        {
            taskId = task.Id,
            projectId = project.Id,
            projectName = project.Name,
            projectColor = project.Color,
            taskTitle = task.Title,
            dueDate = task.DueDate
        });

        var notification = AgentNotificationInbox.Create(
            project.OrgId,
            userId,
            AgentNotificationKind.DeadlineToday,
            "Prazo é hoje",
            $"{project.Name} · {task.Title}",
            meta);

        await _inbox.AddAsync(notification, ct);
        await QueueAgentCommandAsync(userId, "deadline_today", meta, notification.Id, ct);
    }

    public async Task NotifyMembershipChangedAsync(Guid userId, Project project, bool added, CancellationToken ct = default)
    {
        var meta = JsonSerializer.Serialize(new
        {
            projectId = project.Id,
            projectName = project.Name,
            added
        });

        var notification = AgentNotificationInbox.Create(
            project.OrgId,
            userId,
            AgentNotificationKind.ProjectMembershipChanged,
            added ? "Adicionado a um projeto" : "Removido de um projeto",
            project.Name,
            meta);

        await _inbox.AddAsync(notification, ct);
        await QueueAgentCommandAsync(userId, "project_membership_changed", meta, notification.Id, ct);
    }

    /// <summary>
    /// Drops a remote command into the pending-commands queue for every active device of the user.
    /// The agent's RemoteCommandService picks it up on the next sync cycle.
    /// </summary>
    private async Task QueueAgentCommandAsync(Guid userId, string commandType, string payloadJson, Guid notificationId, CancellationToken ct)
    {
        var devices = await _devices.GetActiveByUserIdAsync(userId, ct);
        var actor = _currentUser.UserId ?? userId;

        // Inject the inbox notificationId into the payload so the agent can mark it delivered.
        var payloadWithId = JsonSerializer.Serialize(new
        {
            notificationId,
            payload = JsonDocument.Parse(payloadJson).RootElement
        });

        var user = await _users.GetByIdAsync(userId, ct);
        var orgId = user?.OrgId ?? Guid.Empty;
        if (orgId == Guid.Empty) return;

        foreach (var device in devices)
        {
            var cmd = RemoteCommand.Create(orgId, device.Id, commandType, payloadWithId, actor, TimeSpan.FromHours(24));
            await _commands.AddAsync(cmd, ct);
        }
    }
}
