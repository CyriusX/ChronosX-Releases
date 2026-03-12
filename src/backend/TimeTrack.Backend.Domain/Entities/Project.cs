using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um projeto dentro de uma organização
/// </summary>
public sealed class Project
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Color { get; private set; } = "#4A9FFF";
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public Organization? Organization { get; private set; }

    private Project() { }

    public static Project Create(
        Guid orgId,
        string name,
        string? description = null,
        string color = "#4A9FFF")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(color))
            color = "#4A9FFF";

        return new Project
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Color = color,
            Status = ProjectStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description = null, string? color = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        Color = color ?? Color;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Archive()
    {
        Status = ProjectStatus.Archived;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        Status = ProjectStatus.Active;
        UpdatedAt = DateTime.UtcNow;
    }
}
