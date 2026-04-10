using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class AgentNotificationInboxRepository : IAgentNotificationInboxRepository
{
    private readonly TimeTrackDbContext _context;

    public AgentNotificationInboxRepository(TimeTrackDbContext context) => _context = context;

    public Task<AgentNotificationInbox?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _context.AgentNotificationInbox.FirstOrDefaultAsync(n => n.Id == id, ct);

    public async Task<IReadOnlyList<AgentNotificationInbox>> ListForUserAsync(Guid userId, bool unreadOnly, int take = 50, CancellationToken ct = default)
    {
        var query = _context.AgentNotificationInbox.Where(n => n.UserId == userId);
        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AgentNotificationInbox>> ListUndeliveredForUserAsync(Guid userId, int take = 50, CancellationToken ct = default)
        => await _context.AgentNotificationInbox
            .Where(n => n.UserId == userId && n.DeliveredToAgentAt == null)
            .OrderBy(n => n.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken ct = default)
        => _context.AgentNotificationInbox.CountAsync(n => n.UserId == userId && n.ReadAt == null, ct);

    public async Task AddAsync(AgentNotificationInbox notification, CancellationToken ct = default)
    {
        await _context.AgentNotificationInbox.AddAsync(notification, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AgentNotificationInbox notification, CancellationToken ct = default)
    {
        _context.AgentNotificationInbox.Update(notification);
        await _context.SaveChangesAsync(ct);
    }

    public async Task MarkAllReadAsync(Guid userId, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await _context.AgentNotificationInbox
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, now), ct);
    }

    public async Task<bool> HasNotificationForTaskOnDayAsync(Guid userId, AgentNotificationKind kind, Guid taskId, DateTime utcDate, CancellationToken ct = default)
    {
        var dayStart = new DateTime(utcDate.Year, utcDate.Month, utcDate.Day, 0, 0, 0, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);
        var marker = $"\"taskId\":\"{taskId}\"";

        return await _context.AgentNotificationInbox
            .AnyAsync(n =>
                n.UserId == userId &&
                n.Kind == kind &&
                n.CreatedAt >= dayStart &&
                n.CreatedAt < dayEnd &&
                n.MetadataJson != null &&
                n.MetadataJson.Contains(marker), ct);
    }
}
