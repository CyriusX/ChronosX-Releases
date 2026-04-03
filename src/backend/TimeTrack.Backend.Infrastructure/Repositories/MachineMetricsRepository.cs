using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class MachineMetricsRepository : IMachineMetricsRepository
{
    private readonly TimeTrackDbContext _context;

    public MachineMetricsRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<MachineMetrics?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.MachineMetrics.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<MachineMetrics>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.MachineMetrics.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(MachineMetrics entity, CancellationToken cancellationToken = default)
    {
        await _context.MachineMetrics.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MachineMetrics entity, CancellationToken cancellationToken = default)
    {
        _context.MachineMetrics.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(MachineMetrics entity, CancellationToken cancellationToken = default)
    {
        _context.MachineMetrics.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<MachineMetrics?> GetLatestByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MachineMetrics
            .Where(m => m.DeviceId == deviceId)
            .OrderByDescending(m => m.SampledAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IEnumerable<MachineMetrics>> GetByDeviceIdSinceAsync(
        Guid deviceId,
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        return await _context.MachineMetrics
            .Where(m => m.DeviceId == deviceId && m.SampledAtUtc >= since)
            .OrderBy(m => m.SampledAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        return await _context.MachineMetrics
            .Where(m => m.SampledAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
