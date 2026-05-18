using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IPlatformEventLogRepository
{
    Task AddIfNotExistsAsync(PlatformEventLog entity, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlatformEventLog>> ListAsync(
        DateTime? sinceUtc,
        string? severity,
        int limit,
        CancellationToken cancellationToken = default);
}

