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

    Task<int> CleanupExpiredAsync(CancellationToken cancellationToken = default);
}
