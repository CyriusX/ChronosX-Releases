namespace TimeTrack.Backend.Domain.Entities;

public sealed class OrgUsageRecord
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public int ActiveUsersCount { get; private set; }
    public int ActiveDevicesCount { get; private set; }
    public DateTime LastComputedAt { get; private set; }

    private OrgUsageRecord() { }

    public static OrgUsageRecord Create(Guid orgId)
    {
        return new OrgUsageRecord
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            LastComputedAt = DateTime.UtcNow
        };
    }

    public void UpdateCounts(int activeUsers, int activeDevices)
    {
        ActiveUsersCount = activeUsers;
        ActiveDevicesCount = activeDevices;
        LastComputedAt = DateTime.UtcNow;
    }
}
