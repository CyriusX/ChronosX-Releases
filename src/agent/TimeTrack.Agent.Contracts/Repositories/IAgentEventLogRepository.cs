using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para eventos do agent (SQLite local)
/// </summary>
public interface IAgentEventLogRepository
{
    Task AddAsync(AgentEventLog eventLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AgentEventLog>> GetRecentAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    Task<int> DeleteOlderThanAsync(DateTime cutoff, CancellationToken cancellationToken = default);
}
