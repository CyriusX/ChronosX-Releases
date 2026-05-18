using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IEvidenceItemRepository
{
    Task<EvidenceItem?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<EvidenceItem>> GetByPeriodAsync(
        Guid orgId, Guid? userId, DateTime startDate, DateTime endDate,
        string? evidenceType, int limit, int offset, CancellationToken ct);
    Task AddAsync(EvidenceItem item, CancellationToken ct);
    Task UpdateAsync(EvidenceItem item, CancellationToken ct);
    Task<long> SumFileSizeByOrgAsync(Guid orgId, CancellationToken ct);
    Task<IReadOnlyList<EvidenceItem>> GetExpiredAsync(
        Guid orgId, DateTime cutoffDate, int batchSize, CancellationToken ct);
    Task<int> CountByOrgAsync(Guid orgId, CancellationToken ct);
}
