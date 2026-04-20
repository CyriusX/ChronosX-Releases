using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class StripeEventLogRepository : IStripeEventLogRepository
{
    private readonly TimeTrackDbContext _context;

    public StripeEventLogRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<bool> HasBeenProcessedAsync(string stripeEventId, CancellationToken cancellationToken = default)
    {
        return await _context.StripeEventLogs
            .IgnoreQueryFilters()
            .AnyAsync(e => e.StripeEventId == stripeEventId, cancellationToken);
    }

    public async Task AddAsync(StripeEventLog eventLog, CancellationToken cancellationToken = default)
    {
        await _context.StripeEventLogs.AddAsync(eventLog, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
