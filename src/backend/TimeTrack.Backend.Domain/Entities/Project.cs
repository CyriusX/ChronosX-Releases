using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Representa um projeto dentro de uma organização
/// </summary>
public sealed class Project
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string Color { get; private set; } = "#4A9FFF";
    public ProjectStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }

    // ── Billable project fields ──
    public bool IsBillable { get; private set; }
    public string? Currency { get; private set; }
    public decimal? HourlyRate { get; private set; }

    // ── Linear sync (nullable — only populated on Linear-sourced projects) ──
    public ProjectSyncSource SyncSource { get; private set; } = ProjectSyncSource.Local;
    public string? LinearProjectId { get; private set; }
    public string? LinearWorkspaceId { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    // ── Billable project fields ──
    public bool IsBillable { get; private set; }
    public string? Currency { get; private set; }
    public decimal? HourlyRate { get; private set; }

    // ── Linear sync (nullable — only populated on Linear-sourced projects) ──
    public ProjectSyncSource SyncSource { get; private set; } = ProjectSyncSource.Local;
    public string? LinearProjectId { get; private set; }
    public string? LinearWorkspaceId { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }

    // Navigation properties
    public Organization? Organization { get; private set; }

    private Project() { }

    public static Project Create(
        Guid orgId,
        Guid createdByUserId,
        string name,
        string? description = null,
        string? color = "#4A9FFF",
        bool isBillable = false,
        string? currency = null,
        decimal? hourlyRate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));

        if (string.IsNullOrWhiteSpace(color))
            color = "#4A9FFF";

        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId is required", nameof(createdByUserId));

        if (isBillable)
        {
            if (string.IsNullOrWhiteSpace(currency))
                throw new ArgumentException("Currency is required when project is billable", nameof(currency));
            if (hourlyRate == null || hourlyRate <= 0)
                throw new ArgumentException("Hourly rate must be greater than 0 when project is billable", nameof(hourlyRate));
        }

        return new Project
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CreatedByUserId = createdByUserId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Color = color,
            Status = ProjectStatus.Active,
            SyncSource = ProjectSyncSource.Local,
            CreatedAt = DateTime.UtcNow,
            IsBillable = isBillable,
            Currency = isBillable ? currency?.Trim() : null,
            HourlyRate = isBillable ? hourlyRate : null
        };
    }

    public static Project CreateFromLinear(
        Guid orgId,
        Guid createdByUserId,
        string name,
        string color,
        string linearProjectId,
        string? linearWorkspaceId,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));
        if (string.IsNullOrWhiteSpace(linearProjectId))
            throw new ArgumentException("Linear project id is required", nameof(linearProjectId));
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("CreatedByUserId is required", nameof(createdByUserId));

        return new Project
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            CreatedByUserId = createdByUserId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Color = string.IsNullOrWhiteSpace(color) ? "#4A9FFF" : color,
            Status = ProjectStatus.Active,
            SyncSource = ProjectSyncSource.Linear,
            LinearProjectId = linearProjectId.Trim(),
            LinearWorkspaceId = linearWorkspaceId?.Trim(),
            LastSyncedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(string name, string? description = null, string? color = null, bool? isBillable = null, string? currency = null, decimal? hourlyRate = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));

        Name = name.Trim();
        Description = description?.Trim();
        Color = color ?? Color;

        if (isBillable.HasValue)
        {
            if (isBillable.Value)
            {
                if (string.IsNullOrWhiteSpace(currency))
                    throw new ArgumentException("Currency is required when project is billable", nameof(currency));
                if (hourlyRate == null || hourlyRate <= 0)
                    throw new ArgumentException("Hourly rate must be greater than 0 when project is billable", nameof(hourlyRate));
                IsBillable = true;
                Currency = currency?.Trim();
                HourlyRate = hourlyRate;
            }
            else
            {
                IsBillable = false;
                Currency = null;
                HourlyRate = null;
            }
        }
        else if (IsBillable)
        {
            if (currency != null)
                Currency = currency.Trim();
            if (hourlyRate.HasValue && hourlyRate.Value > 0)
                HourlyRate = hourlyRate.Value;
        }

        UpdatedAt = DateTime.UtcNow;
    }

    public void ApplyLinearSnapshot(string name, string color, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required", nameof(name));

        Name = name.Trim();
        if (!string.IsNullOrWhiteSpace(color))
            Color = color;
        Description = description?.Trim();
        LastSyncedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete(Guid deletedByUserId)
    {
        if (deletedByUserId == Guid.Empty)
            throw new ArgumentException("DeletedByUserId is required", nameof(deletedByUserId));

        var now = DateTime.UtcNow;
        DeletedAt = now;
        DeletedByUserId = deletedByUserId;
        UpdatedAt = now;
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
