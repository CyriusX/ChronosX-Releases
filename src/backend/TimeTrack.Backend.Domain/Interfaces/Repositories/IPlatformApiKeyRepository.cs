using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IPlatformApiKeyRepository : IRepository<PlatformApiKey>
{
    Task<PlatformApiKey?> GetByKeyHashAsync(string keyHash, CancellationToken cancellationToken = default);
}

