using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for organization-specific category overrides
///
/// SRP: Apenas define operações para overrides por organização
/// ISP: Interface segregada - apenas métodos necessários
/// </summary>
public interface IAppCategoryOverrideRepository
{
    /// <summary>
    /// Find an override for a specific org and identifier
    /// </summary>
    Task<AppCategoryOverride?> FindAsync(
        Guid orgId,
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all overrides for an organization
    /// </summary>
    Task<IReadOnlyList<AppCategoryOverride>> GetByOrgIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get overrides by productivity level for an organization
    /// </summary>
    Task<IReadOnlyList<AppCategoryOverride>> GetByOrgAndProductivityAsync(
        Guid orgId,
        AppProductivityCategory productivity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup - get multiple overrides at once for an org
    /// </summary>
    Task<IReadOnlyList<AppCategoryOverride>> GetByOrgAndIdentifiersAsync(
        Guid orgId,
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an override exists for an org and identifier
    /// </summary>
    Task<bool> ExistsAsync(
        Guid orgId,
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new override
    /// </summary>
    Task<AppCategoryOverride> AddAsync(
        AppCategoryOverride categoryOverride,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Update an existing override
    /// </summary>
    Task UpdateAsync(
        AppCategoryOverride categoryOverride,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an override by ID
    /// </summary>
    Task<bool> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete an override by org and identifier
    /// </summary>
    Task<bool> DeleteByOrgAndIdentifierAsync(
        Guid orgId,
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of overrides for an organization
    /// </summary>
    Task<int> CountByOrgIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default);
}
