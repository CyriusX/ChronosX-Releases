using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Repositório para períodos de inatividade
/// </summary>
public interface IIdlePeriodRepository
{
    Task<IReadOnlyList<IdlePeriod>> GetByDateAsync(Guid userId, DateTime date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IdlePeriod>> GetByDateRangeAsync(Guid userId, DateTime start, DateTime end, CancellationToken cancellationToken = default);
    Task SaveAsync(IdlePeriod period, CancellationToken cancellationToken = default);
    Task SaveBatchAsync(IEnumerable<IdlePeriod> periods, CancellationToken cancellationToken = default);
}
