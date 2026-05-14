namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Current platform health state used for transitions (healthy/degraded/unhealthy).
/// </summary>
public sealed class PlatformHealthState
{
    public Guid Id { get; private set; }
    public string Status { get; private set; } = "healthy";
    public string ChecksJson { get; private set; } = "{}";
    public DateTime LastChangedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PlatformHealthState() { }

    public static PlatformHealthState CreateInitial(Guid id)
    {
        return new PlatformHealthState
        {
            Id = id,
            Status = "healthy",
            ChecksJson = "{}",
            LastChangedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }

    public bool Set(string status, string checksJson)
    {
        status = string.IsNullOrWhiteSpace(status) ? "healthy" : status.Trim().ToLowerInvariant();
        checksJson = string.IsNullOrWhiteSpace(checksJson) ? "{}" : checksJson;

        var changed = !string.Equals(Status, status, StringComparison.OrdinalIgnoreCase);
        Status = status;
        ChecksJson = checksJson;
        UpdatedAtUtc = DateTime.UtcNow;
        if (changed)
        {
            LastChangedAtUtc = UpdatedAtUtc;
        }
        return changed;
    }
}

