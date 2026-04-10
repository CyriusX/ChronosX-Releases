using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IProjectMemberRepository
{
    Task<ProjectMember?> GetAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> IsMemberAsync(Guid projectId, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectMember>> ListByProjectAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListProjectIdsForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(ProjectMember member, CancellationToken cancellationToken = default);
    Task RemoveAsync(ProjectMember member, CancellationToken cancellationToken = default);
}
