using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa uma organização (tenant) no sistema
/// </summary>
public sealed class Organization
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public OrgType OrgType { get; private set; }
    public OrgStatus Status { get; private set; }
    public decimal StorageQuotaGb { get; private set; } = 10m;
    public long StorageUsedBytes { get; private set; } = 0;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? OnboardingCompletedAt { get; private set; }

    // Navigation properties
    private readonly List<User> _users = new();
    public IReadOnlyCollection<User> Users => _users.AsReadOnly();

    private readonly List<Device> _devices = new();
    public IReadOnlyCollection<Device> Devices => _devices.AsReadOnly();

    private Organization() { }

    public static Organization Create(string name, string slug, OrgType orgType)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Organization slug is required", nameof(slug));

        return new Organization
        {
            Id = Guid.NewGuid(),
            Name = name,
            Slug = slug.ToLowerInvariant(),
            OrgType = orgType,
            Status = OrgStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organization name is required", nameof(name));

        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        Status = OrgStatus.Inactive;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = OrgStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetStorageQuota(decimal quotaGb)
    {
        if (quotaGb < 1m || quotaGb > 100m)
            throw new ArgumentOutOfRangeException(nameof(quotaGb), "Quota must be between 1 and 100 GB");
        StorageQuotaGb = quotaGb;
        UpdatedAt = DateTime.UtcNow;
    }

    public void IncrementStorageUsage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        StorageUsedBytes += bytes;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DecrementStorageUsage(long bytes)
    {
        if (bytes < 0) throw new ArgumentOutOfRangeException(nameof(bytes));
        StorageUsedBytes = Math.Max(0, StorageUsedBytes - bytes);
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsStorageQuotaExceeded()
    {
        var quotaBytes = (long)(StorageQuotaGb * 1024m * 1024m * 1024m);
        return StorageUsedBytes >= quotaBytes;
    }

    public double GetStorageUsagePercentage()
    {
        var quotaBytes = (long)(StorageQuotaGb * 1024m * 1024m * 1024m);
        return quotaBytes > 0 ? (double)StorageUsedBytes / quotaBytes * 100 : 0;
    }

    public void CompleteOnboarding()
    {
        OnboardingCompletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}
