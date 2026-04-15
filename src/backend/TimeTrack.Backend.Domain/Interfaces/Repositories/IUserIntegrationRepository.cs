using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IUserIntegrationRepository
{
    Task<UserIntegration?> GetAsync(Guid userId, UserIntegrationProvider provider, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserIntegration>> ListByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(UserIntegration integration, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserIntegration integration, CancellationToken cancellationToken = default);
    Task DeleteAsync(UserIntegration integration, CancellationToken cancellationToken = default);
}
