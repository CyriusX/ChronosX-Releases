using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Entities;

/// <summary>
/// Membership of a user in a project. Only members can be assigned tasks.
/// </summary>
public sealed class ProjectMember
{
    public Guid Id { get; private set; }
    public Guid OrgId { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public ProjectMemberRole Role { get; private set; }
    public DateTime AddedAt { get; private set; }
    public Guid AddedByUserId { get; private set; }

    public Project? Project { get; private set; }
    public User? User { get; private set; }

    private ProjectMember() { }

    public static ProjectMember Create(
        Guid orgId,
        Guid projectId,
        Guid userId,
        Guid addedByUserId,
        ProjectMemberRole role = ProjectMemberRole.Member)
    {
        return new ProjectMember
        {
            Id = Guid.NewGuid(),
            OrgId = orgId,
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            AddedAt = DateTime.UtcNow,
            AddedByUserId = addedByUserId
        };
    }

    public void ChangeRole(ProjectMemberRole role)
    {
        Role = role;
    }
}
