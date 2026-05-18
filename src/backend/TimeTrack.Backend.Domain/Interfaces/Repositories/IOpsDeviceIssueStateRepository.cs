using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IOpsDeviceIssueStateRepository
{
    Task<OpsDeviceIssueState?> GetByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default);
    Task UpsertAsync(OpsDeviceIssueState state, CancellationToken cancellationToken = default);
}

