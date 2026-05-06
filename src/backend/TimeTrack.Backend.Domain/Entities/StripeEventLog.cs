namespace TimeTrack.Backend.Domain.Entities;

public sealed class StripeEventLog
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string StripeEventId { get; private set; } = string.Empty;
    public string EventType { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; }
    public string? PayloadHash { get; private set; }
    public string Status { get; private set; } = string.Empty;
    public string? ErrorMessage { get; private set; }

    private StripeEventLog() { }

    public static StripeEventLog Create(
        Guid orgId,
        string stripeEventId,
        string eventType,
        string status,
        string? payloadHash = null,
        string? errorMessage = null)
    {
        return new StripeEventLog
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            StripeEventId = stripeEventId,
            EventType = eventType,
            Status = status,
            PayloadHash = payloadHash,
            ErrorMessage = errorMessage,
            ProcessedAt = DateTime.UtcNow
        };
    }
}
