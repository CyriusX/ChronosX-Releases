namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Comando remoto enviado pelo admin para um dispositivo/agent
/// </summary>
public sealed class RemoteCommand
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public string CommandType { get; private set; } = string.Empty;
    public string? PayloadJson { get; private set; }
    public string Status { get; private set; } = "pending";
    public string? ResultJson { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }

    // Navigation
    public Device? Device { get; private set; }

    private RemoteCommand() { }

    public static RemoteCommand Create(
        Guid orgId,
        Guid deviceId,
        string commandType,
        string? payloadJson,
        Guid createdByUserId,
        TimeSpan? ttl = null)
    {
        var now = DateTime.UtcNow;
        return new RemoteCommand
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            DeviceId = deviceId,
            CommandType = commandType,
            PayloadJson = payloadJson,
            Status = "pending",
            CreatedByUserId = createdByUserId,
            CreatedAt = now,
            ExpiresAt = now.Add(ttl ?? TimeSpan.FromMinutes(5))
        };
    }

    public void MarkCompleted(string? resultJson = null)
    {
        Status = "completed";
        ResultJson = resultJson;
        AcknowledgedAt = DateTime.UtcNow;
    }

    public void MarkFailed(string errorMessage)
    {
        Status = "failed";
        ResultJson = errorMessage;
        AcknowledgedAt = DateTime.UtcNow;
    }

    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
