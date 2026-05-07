using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class OrgUsageRecordRepository : IOrgUsageRecordRepository
{
    private readonly TimeTrackDbContext _context;

    public OrgUsageRecordRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<OrgUsageRecord?> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.OrgUsageRecords
            .FirstOrDefaultAsync(r => r.OrgId == orgId, cancellationToken);
    }

    public async Task<OrgUsageRecord> GetOrCreateAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        var record = await _context.OrgUsageRecords
            .FirstOrDefaultAsync(r => r.OrgId == orgId, cancellationToken);

        if (record is not null)
            return record;

        record = OrgUsageRecord.Create(orgId);
        await _context.OrgUsageRecords.AddAsync(record, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return record;
    }

    public async Task UpdateAsync(OrgUsageRecord record, CancellationToken cancellationToken = default)
    {
        _context.OrgUsageRecords.Update(record);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
