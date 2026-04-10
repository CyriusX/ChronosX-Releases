using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
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
}
