using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Per-user persistent notification inbox. Surfaced via the bell icon
/// on the desktop agent so users see things they missed while away.
/// </summary>
public sealed class AgentNotificationInbox
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid UserId { get; private set; }
    public AgentNotificationKind Kind { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;
    public string? MetadataJson { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? DeliveredToAgentAt { get; private set; }

    public User? User { get; private set; }

    private AgentNotificationInbox() { }

    public static AgentNotificationInbox Create(
        Guid orgId,
        Guid userId,
        AgentNotificationKind kind,
        string title,
        string body,
        string? metadataJson = null)
    {
        return new AgentNotificationInbox
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            UserId = userId,
            Kind = kind,
            Title = title,
            Body = body,
            MetadataJson = metadataJson,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRead()
    {
        ReadAt ??= DateTime.UtcNow;
    }

    public void MarkDelivered()
    {
        DeliveredToAgentAt ??= DateTime.UtcNow;
    }

    public bool IsUnread => ReadAt is null;
}
