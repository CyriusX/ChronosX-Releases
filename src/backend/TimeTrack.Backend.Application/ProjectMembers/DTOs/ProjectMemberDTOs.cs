using System.ComponentModel.DataAnnotations;

namespace TimeTrack.Backend.Application.ProjectMembers.DTOs;

public sealed class AddProjectMemberRequest
{
    [Required]
    public Guid UserId { get; init; }

    public string Role { get; init; } = "Member"; // Member | Owner
}

public sealed class ProjectMemberResponse
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public Guid UserId { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime AddedAt { get; init; }
}

public sealed class ListProjectMembersResponse
{
    public List<ProjectMemberResponse> Members { get; init; } = [];
    public int TotalCount { get; init; }
}
