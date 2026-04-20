using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class DeviceRepository : IDeviceRepository
{
    private readonly TimeTrackDbContext _context;

    public DeviceRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<Device?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Device?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .FirstOrDefaultAsync(d => d.UserId == userId, cancellationToken);
    }

    public async Task<Device?> GetByIdUnfilteredAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .IgnoreQueryFilters()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetActiveByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Where(d => d.UserId == userId && d.Status == Domain.ValueObjects.DeviceStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Device>> GetActiveByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .Include(d => d.User)
            .Where(d => d.OrgId == orgId && d.Status == Domain.ValueObjects.DeviceStatus.Active)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> IdExistsAsync(Guid deviceId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices.AnyAsync(d => d.Id == deviceId, cancellationToken);
    }

    public async Task AddAsync(Device device, CancellationToken cancellationToken = default)
    {
        await _context.Devices.AddAsync(device, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Device device, CancellationToken cancellationToken = default)
    {
        _context.Devices.Update(device);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> CountActiveByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default)
    {
        return await _context.Devices
            .CountAsync(d => d.OrgId == orgId, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var device = await _context.Devices.FindAsync([id], cancellationToken);
        if (device is not null)
        {
            _context.Devices.Remove(device);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
