using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IAgentEventLogRepository : IRepository<AgentEventLog>
{
    Task<IEnumerable<AgentEventLog>> GetByDeviceIdAsync(
        Guid deviceId,
        string? category,
        string? severity,
        DateTime? since,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default);
}
