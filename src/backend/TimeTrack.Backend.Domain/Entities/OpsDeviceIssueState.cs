namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Tracks last-known "critical issue" state for a device to detect transitions (offline/unhealthy/degraded/none).
/// </summary>
public sealed class OpsDeviceIssueState
{
    public Guid DeviceId { get; private set; }
    public Guid OrgId { get; private set; }
    public string Issue { get; private set; } = "none";
    public bool IsActive { get; private set; }
    public DateTime LastTransitionAtUtc { get; private set; }
    public DateTime? LastNotifiedAtUtc { get; private set; }

    private OpsDeviceIssueState() { }

    public static OpsDeviceIssueState Create(Guid deviceId, Guid orgId, string issue, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(issue)) issue = "none";

        return new OpsDeviceIssueState
        {
            DeviceId = deviceId,
            OrgId = orgId,
            Issue = issue.Trim(),
            IsActive = isActive,
            LastTransitionAtUtc = DateTime.UtcNow,
        };
    }

    public bool TransitionTo(string issue, bool isActive)
    {
        issue = string.IsNullOrWhiteSpace(issue) ? "none" : issue.Trim();

        if (Issue == issue && IsActive == isActive)
            return false;

        Issue = issue;
        IsActive = isActive;
        LastTransitionAtUtc = DateTime.UtcNow;
        return true;
    }

    public void MarkNotified()
    {
        LastNotifiedAtUtc = DateTime.UtcNow;
    }
}

