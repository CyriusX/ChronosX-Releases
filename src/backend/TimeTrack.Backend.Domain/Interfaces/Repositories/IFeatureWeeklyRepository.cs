using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IFeatureWeeklyRepository : IRepository<FeatureWeekly>
{
    Task<FeatureWeekly?> GetByUserAndWeekAsync(Guid userId, DateOnly weekStart, CancellationToken ct = default);
    Task<IEnumerable<FeatureWeekly>> GetByUserAndDateRangeAsync(Guid userId, DateOnly start, DateOnly end, CancellationToken ct = default);
    Task<IEnumerable<FeatureWeekly>> GetByOrgAndWeekAsync(Guid orgId, DateOnly weekStart, CancellationToken ct = default);
    Task UpsertAsync(FeatureWeekly entity, CancellationToken ct = default);
}
