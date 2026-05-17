namespace TimeTrack.Backend.Application.Maintenance.DTOs;

public sealed record OrganizationListItem
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string OrgType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public int UserCount { get; init; }
    public int DeviceCount { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed record ListOrganizationsResponse
{
    public List<OrganizationListItem> Organizations { get; init; } = new();
    public int TotalCount { get; init; }
}
