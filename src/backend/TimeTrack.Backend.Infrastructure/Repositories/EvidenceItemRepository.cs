using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class EvidenceItemRepository : IEvidenceItemRepository
{
    private readonly TimeTrackDbContext _context;

    public EvidenceItemRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<EvidenceItem?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _context.EvidenceItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == id, ct);
    }

    public async Task<IReadOnlyList<EvidenceItem>> GetByPeriodAsync(
        Guid orgId, Guid? userId, DateTime startDate, DateTime endDate,
        string? evidenceType, int limit, int offset, CancellationToken ct)
    {
        var query = _context.EvidenceItems
            .Where(e => e.OrgId == orgId
                && e.CapturedAt >= startDate
                && e.CapturedAt <= endDate
                && !e.IsDeleted);

        if (userId.HasValue)
            query = query.Where(e => e.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(evidenceType))
            query = query.Where(e => e.EvidenceType == evidenceType);

        return await query
            .OrderByDescending(e => e.CapturedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task AddAsync(EvidenceItem item, CancellationToken ct)
    {
        await _context.EvidenceItems.AddAsync(item, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(EvidenceItem item, CancellationToken ct)
    {
        _context.EvidenceItems.Update(item);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<long> SumFileSizeByOrgAsync(Guid orgId, CancellationToken ct)
    {
        return await _context.EvidenceItems
            .IgnoreQueryFilters()
            .Where(e => e.OrgId == orgId && !e.IsDeleted)
            .SumAsync(e => e.FileSizeBytes, ct);
    }

    public async Task<IReadOnlyList<EvidenceItem>> GetExpiredAsync(
        Guid orgId, DateTime cutoffDate, int batchSize, CancellationToken ct)
    {
        return await _context.EvidenceItems
            .IgnoreQueryFilters()
            .Where(e => e.OrgId == orgId && e.CapturedAt < cutoffDate && !e.IsDeleted)
            .OrderBy(e => e.CapturedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<int> CountByOrgAsync(Guid orgId, CancellationToken ct)
    {
        return await _context.EvidenceItems
            .IgnoreQueryFilters()
            .CountAsync(e => e.OrgId == orgId && !e.IsDeleted, ct);
    }
}
