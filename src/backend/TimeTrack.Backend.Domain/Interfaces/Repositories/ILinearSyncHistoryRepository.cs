using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface ILinearSyncHistoryRepository
{
    Task<IReadOnlyList<LinearSyncHistory>> ListRecentForUserAsync(Guid userId, int take = 20, CancellationToken cancellationToken = default);
    Task AddAsync(LinearSyncHistory entry, CancellationToken cancellationToken = default);
}
