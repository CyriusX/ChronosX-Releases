using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa uma política de trabalho da organização
/// </summary>
public sealed class Policy
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public PolicyType Type { get; private set; }
    public string ConfigJson { get; private set; } = "{}";
    public bool IsEnabled { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public Organization? Organization { get; private set; }

    private Policy() { }

    public static Policy Create(
        Guid orgId,
        string name,
        PolicyType type,
        string configJson)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Policy name is required", nameof(name));

        return new Policy
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = name,
            Type = type,
            ConfigJson = configJson,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string configJson, bool isEnabled)
    {
        Name = name;
        ConfigJson = configJson;
        IsEnabled = isEnabled;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Enable()
    {
        IsEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Disable()
    {
        IsEnabled = false;
        UpdatedAt = DateTime.UtcNow;
    }
}
