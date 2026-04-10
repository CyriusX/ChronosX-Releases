using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IAgentNotificationInboxRepository
{
    Task<AgentNotificationInbox?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentNotificationInbox>> ListForUserAsync(Guid userId, bool unreadOnly, int take = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgentNotificationInbox>> ListUndeliveredForUserAsync(Guid userId, int take = 50, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(AgentNotificationInbox notification, CancellationToken cancellationToken = default);
    Task UpdateAsync(AgentNotificationInbox notification, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
