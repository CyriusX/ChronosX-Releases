using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IPlatformHealthStateRepository
{
    Task<PlatformHealthState> GetAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(PlatformHealthState entity, CancellationToken cancellationToken = default);
}

