using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IOrgUsageRecordRepository
{
    Task<OrgUsageRecord?> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<OrgUsageRecord> GetOrCreateAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task UpdateAsync(OrgUsageRecord record, CancellationToken cancellationToken = default);
}
