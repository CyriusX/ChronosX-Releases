using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class OpsDeviceIssueStateRepository : IOpsDeviceIssueStateRepository
{
    private readonly TimeTrackDbContext _context;

    public OpsDeviceIssueStateRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<OpsDeviceIssueState?> GetByDeviceIdAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.OpsDeviceIssueStates.FirstOrDefaultAsync(s => s.DeviceId == deviceId, cancellationToken);
    }

    public async Task UpsertAsync(OpsDeviceIssueState state, CancellationToken cancellationToken = default)
    {
        var existing = await _context.OpsDeviceIssueStates
            .FirstOrDefaultAsync(s => s.DeviceId == state.DeviceId, cancellationToken);

        if (existing is null)
        {
            await _context.OpsDeviceIssueStates.AddAsync(state, cancellationToken);
        }
        else
        {
            // Apply current values via domain methods (private setters).
            existing.TransitionTo(state.Issue, state.IsActive);
            if (state.LastNotifiedAtUtc.HasValue)
            {
                existing.MarkNotified();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
