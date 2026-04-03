using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IRemoteCommandRepository
{
    Task<IEnumerable<RemoteCommand>> GetPendingByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken = default);

    Task<IEnumerable<RemoteCommand>> GetByDeviceIdAsync(
        Guid deviceId, int limit = 20, CancellationToken cancellationToken = default);

    Task<RemoteCommand?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default);

    Task AddAsync(RemoteCommand command, CancellationToken cancellationToken = default);

    Task UpdateAsync(RemoteCommand command, CancellationToken cancellationToken = default);
}
