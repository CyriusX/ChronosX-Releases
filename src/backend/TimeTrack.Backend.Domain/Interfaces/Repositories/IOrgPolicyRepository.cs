using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for OrgPolicy
/// </summary>
public interface IOrgPolicyRepository
{
    /// <summary>
    /// Gets the policy for a specific organization
    /// </summary>
    Task<OrgPolicy?> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a policy by its ID
    /// </summary>
    Task<OrgPolicy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new policy
    /// </summary>
    Task AddAsync(OrgPolicy policy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing policy
    /// </summary>
    Task UpdateAsync(OrgPolicy policy, CancellationToken cancellationToken = default);
}
