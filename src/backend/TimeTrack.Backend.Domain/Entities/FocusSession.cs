using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa uma sessão de foco sincronizada do Agent
/// </summary>
public sealed class FocusSession
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid DeviceId { get; private set; }
    public Guid UserId { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? EndedAt { get; private set; }
    public int? PlannedDurationMinutes { get; private set; }
    public int? ActualDurationMinutes { get; private set; }
    public FocusSessionStatus Status { get; private set; }
    public int? FocusScore { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    // Navigation properties
    public Device? Device { get; private set; }
    public User? User { get; private set; }

    private FocusSession() { }

    public static FocusSession Create(
        Guid id,
        Guid orgId,
        Guid deviceId,
        Guid userId,
        DateTime startedAt,
        int plannedDurationMinutes,
        string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        return new FocusSession
        {
            Id = id,
            OrgId = orgId,
            DeviceId = deviceId,
            UserId = userId,
            StartedAt = startedAt,
            PlannedDurationMinutes = plannedDurationMinutes,
            Status = FocusSessionStatus.InProgress,
            IdempotencyKey = idempotencyKey,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Complete(DateTime endedAt, int focusScore)
    {
        EndedAt = endedAt;
        ActualDurationMinutes = (int)(endedAt - StartedAt).TotalMinutes;
        FocusScore = Math.Clamp(focusScore, 0, 100);
        Status = FocusSessionStatus.Completed;
    }

    public void Cancel(DateTime endedAt)
    {
        EndedAt = endedAt;
        ActualDurationMinutes = (int)(endedAt - StartedAt).TotalMinutes;
        Status = FocusSessionStatus.Cancelled;
    }
}
