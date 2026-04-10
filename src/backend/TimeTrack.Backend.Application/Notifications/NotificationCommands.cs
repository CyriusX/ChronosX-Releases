using MediatR;
using TimeTrack.Backend.Application.Common.Exceptions;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Notifications;

// ═══════════════════════════════════════════════════════════════════════════
// LIST NOTIFICATIONS
// ═══════════════════════════════════════════════════════════════════════════

public sealed record ListMyNotificationsQuery(bool UnreadOnly = false, int Take = 50) : IRequest<ListNotificationsResponse>;

public sealed class ListMyNotificationsQueryHandler : IRequestHandler<ListMyNotificationsQuery, ListNotificationsResponse>
{
    private readonly IAgentNotificationInboxRepository _inbox;
    private readonly ICurrentUserContext _currentUser;

    public ListMyNotificationsQueryHandler(IAgentNotificationInboxRepository inbox, ICurrentUserContext currentUser)
    {
        _inbox = inbox;
        _currentUser = currentUser;
    }

    public async Task<ListNotificationsResponse> Handle(ListMyNotificationsQuery request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var notifications = await _inbox.ListForUserAsync(_currentUser.UserId.Value, request.UnreadOnly, request.Take, ct);
        var unread = await _inbox.CountUnreadAsync(_currentUser.UserId.Value, ct);

        var items = notifications.Select(n => new NotificationItem
        {
            Id = n.Id,
            Kind = n.Kind.ToString(),
            Title = n.Title,
            Body = n.Body,
            MetadataJson = n.MetadataJson,
            CreatedAt = n.CreatedAt,
            ReadAt = n.ReadAt
        }).ToList();

        return new ListNotificationsResponse
        {
            Notifications = items,
            UnreadCount = unread,
            TotalCount = items.Count
        };
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// MARK READ
// ═══════════════════════════════════════════════════════════════════════════

public sealed record MarkNotificationReadCommand(Guid NotificationId) : IRequest<Unit>;

public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand, Unit>
{
    private readonly IAgentNotificationInboxRepository _inbox;
    private readonly ICurrentUserContext _currentUser;

    public MarkNotificationReadCommandHandler(IAgentNotificationInboxRepository inbox, ICurrentUserContext currentUser)
    {
        _inbox = inbox;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();

        var n = await _inbox.GetByIdAsync(request.NotificationId, ct)
            ?? throw new NotFoundException("Notification", request.NotificationId);

        if (n.UserId != _currentUser.UserId.Value)
            throw new ForbiddenException("Notification does not belong to this user");

        n.MarkRead();
        await _inbox.UpdateAsync(n, ct);
        return Unit.Value;
    }
}

public sealed record MarkAllNotificationsReadCommand : IRequest<Unit>;

public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand, Unit>
{
    private readonly IAgentNotificationInboxRepository _inbox;
    private readonly ICurrentUserContext _currentUser;

    public MarkAllNotificationsReadCommandHandler(IAgentNotificationInboxRepository inbox, ICurrentUserContext currentUser)
    {
        _inbox = inbox;
        _currentUser = currentUser;
    }

    public async Task<Unit> Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
    {
        if (!_currentUser.UserId.HasValue) throw new UnauthorizedAccessException();
        await _inbox.MarkAllReadAsync(_currentUser.UserId.Value, ct);
        return Unit.Value;
    }
}
