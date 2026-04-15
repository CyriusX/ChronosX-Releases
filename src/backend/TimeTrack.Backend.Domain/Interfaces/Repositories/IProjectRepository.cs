using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdWithOrgAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Project>> GetActiveByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsInOrgAsync(string name, Guid orgId, Guid? excludeProjectId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task DeleteAsync(Project project, CancellationToken cancellationToken = default);

    /// <summary>Find a Linear-sourced project by its external identifier.</summary>
    Task<Project?> GetByLinearProjectIdAsync(Guid orgId, string linearProjectId, CancellationToken cancellationToken = default);
}
