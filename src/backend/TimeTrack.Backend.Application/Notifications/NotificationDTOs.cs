namespace TimeTrack.Backend.Application.Notifications;

public sealed class NotificationItem
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string? MetadataJson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public sealed class ListNotificationsResponse
{
    public List<NotificationItem> Notifications { get; init; } = [];
    public int UnreadCount { get; init; }
    public int TotalCount { get; init; }
}
