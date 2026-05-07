namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um período de inatividade sincronizado do Agent
/// </summary>
public sealed class IdlePeriod
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime EndedAt { get; private set; }
    public int DurationSeconds { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public string? JustificationReasonCode { get; private set; }
    public string? JustificationNote { get; private set; }
    public DateTime? JustificationSubmittedAtUtc { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public Device? Device { get; private set; }
    public User? User { get; private set; }

    private IdlePeriod() { }

    public static IdlePeriod Create(
        Guid id,
        Guid orgId,
        Guid deviceId,
        Guid userId,
        DateTime startedAt,
        DateTime endedAt,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        if (endedAt < startedAt)
            throw new ArgumentException("End time must be after start time");

        return new IdlePeriod
        {
            Id = id,
            OrgId = orgId,
            DeviceId = deviceId,
            UserId = userId,
            StartedAt = startedAt,
            EndedAt = endedAt,
            DurationSeconds = (int)(endedAt - startedAt).TotalSeconds,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void SubmitJustification(string reasonCode, string? note, DateTime submittedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
            throw new ArgumentException("Reason code is required", nameof(reasonCode));

        if (!string.IsNullOrWhiteSpace(note) && note.Length > 500)
            throw new ArgumentOutOfRangeException(nameof(note), "Idle justification note must be 500 characters or fewer");

        JustificationReasonCode = reasonCode.Trim();
        JustificationNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        JustificationSubmittedAtUtc = submittedAtUtc;
    }
}
