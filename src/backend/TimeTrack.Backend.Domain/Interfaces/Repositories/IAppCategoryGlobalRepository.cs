using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

/// <summary>
/// Repository interface for global app categories
///
/// SRP: Apenas define operações de leitura para categorias globais
/// ISP: Interface segregada - apenas métodos necessários
/// </summary>
public interface IAppCategoryGlobalRepository
{
    /// <summary>
    /// Find a global category by identifier
    /// </summary>
    Task<AppCategoryGlobal?> FindByIdentifierAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all global categories
    /// </summary>
    Task<IReadOnlyList<AppCategoryGlobal>> GetAllAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Search global categories by display name or identifier
    /// </summary>
    Task<IReadOnlyList<AppCategoryGlobal>> SearchAsync(
        string searchTerm,
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get categories by productivity level
    /// </summary>
    Task<IReadOnlyList<AppCategoryGlobal>> GetByProductivityAsync(
        AppProductivityCategory productivity,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch lookup - get multiple categories at once
    /// </summary>
    Task<IReadOnlyList<AppCategoryGlobal>> GetByIdentifiersAsync(
        IEnumerable<string> identifiers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an identifier exists in the global list
    /// </summary>
    Task<bool> ExistsAsync(
        string identifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new global category (for seeding/maintenance)
    /// </summary>
    Task<AppCategoryGlobal> AddAsync(
        AppCategoryGlobal category,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Add multiple categories (for seeding)
    /// </summary>
    Task AddRangeAsync(
        IEnumerable<AppCategoryGlobal> categories,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of global categories
    /// </summary>
    Task<int> CountAsync(
        CancellationToken cancellationToken = default);
}
