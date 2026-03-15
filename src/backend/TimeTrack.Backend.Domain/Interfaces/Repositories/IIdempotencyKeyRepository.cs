using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IIdempotencyKeyRepository : IRepository<IdempotencyKey>
{
    Task<IdempotencyKey?> GetByKeyAsync(
        string key,
        string entityType,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(
        string key,
        string entityType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch check - returns set of existing keys for faster processing
    /// </summary>
    Task<HashSet<string>> GetExistingKeysAsync(
        IEnumerable<string> keys,
        string entityType,
        CancellationToken cancellationToken = default);

    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
