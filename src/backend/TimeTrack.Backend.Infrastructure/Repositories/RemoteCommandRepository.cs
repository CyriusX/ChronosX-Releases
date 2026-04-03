using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class RemoteCommandRepository : IRemoteCommandRepository
{
    private readonly TimeTrackDbContext _context;

    public RemoteCommandRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<RemoteCommand>> GetPendingByDeviceIdAsync(
        Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.RemoteCommands
            .Where(rc => rc.DeviceId == deviceId
                      && rc.Status == "pending"
                      && rc.ExpiresAt > DateTime.UtcNow)
            .OrderBy(rc => rc.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<RemoteCommand>> GetByDeviceIdAsync(
        Guid deviceId, int limit = 20, CancellationToken cancellationToken = default)
    {
        return await _context.RemoteCommands
            .Where(rc => rc.DeviceId == deviceId)
            .OrderByDescending(rc => rc.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<RemoteCommand?> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.RemoteCommands.FindAsync([id], cancellationToken);
    }

    public async Task AddAsync(RemoteCommand command, CancellationToken cancellationToken = default)
    {
        await _context.RemoteCommands.AddAsync(command, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RemoteCommand command, CancellationToken cancellationToken = default)
    {
        _context.RemoteCommands.Update(command);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
