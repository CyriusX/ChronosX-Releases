using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IFeatureMonthlyRepository : IRepository<FeatureMonthly>
{
    Task<FeatureMonthly?> GetByUserAndMonthAsync(Guid userId, DateOnly monthStart, CancellationToken ct = default);
    Task<IEnumerable<FeatureMonthly>> GetByUserAndDateRangeAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken ct = default);
    Task UpsertAsync(FeatureMonthly entity, CancellationToken ct = default);
}
