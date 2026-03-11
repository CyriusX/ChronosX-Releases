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
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

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
}
