using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class AgentEventLogRepository : IAgentEventLogRepository
{
    private readonly TimeTrackDbContext _context;

    public AgentEventLogRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<AgentEventLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.AgentEventLogs.FindAsync([id], cancellationToken);
    }

    public async Task<IEnumerable<AgentEventLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AgentEventLogs.ToListAsync(cancellationToken);
    }

    public async Task AddAsync(AgentEventLog entity, CancellationToken cancellationToken = default)
    {
        await _context.AgentEventLogs.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(AgentEventLog entity, CancellationToken cancellationToken = default)
    {
        _context.AgentEventLogs.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(AgentEventLog entity, CancellationToken cancellationToken = default)
    {
        _context.AgentEventLogs.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<AgentEventLog>> GetByDeviceIdAsync(
        Guid deviceId,
        string? category,
        string? severity,
        DateTime? since,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AgentEventLogs
            .Where(e => e.DeviceId == deviceId);

        if (!string.IsNullOrEmpty(category))
            query = query.Where(e => e.Category == category);

        if (!string.IsNullOrEmpty(severity))
            query = query.Where(e => e.Severity == severity);

        if (since.HasValue)
            query = query.Where(e => e.TimestampUtc >= since.Value);

        return await query
            .OrderByDescending(e => e.TimestampUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(
        DateTime cutoff,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentEventLogs
            .Where(e => e.TimestampUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteByDeviceIdAsync(
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.AgentEventLogs
            .Where(e => e.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteByOrgIdAsync(
        Guid orgId,
        CancellationToken cancellationToken = default)
    {
        // AgentEventLog -> Device -> OrgId via EF navigation or raw filter on DeviceId set
        var deviceIds = await _context.Devices
            .Where(d => d.OrgId == orgId)
            .Select(d => d.Id)
            .ToListAsync(cancellationToken);

        return await _context.AgentEventLogs
            .Where(e => deviceIds.Contains(e.DeviceId))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
