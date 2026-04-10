using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.ValueObjects;

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

    /// <summary>
    /// Returns true if a notification of the given kind referencing the given taskId has already
    /// been inserted for this user on the given UTC calendar day. Used by DeadlineScanJob to avoid
    /// spamming the bell hourly.
    /// </summary>
    Task<bool> HasNotificationForTaskOnDayAsync(Guid userId, AgentNotificationKind kind, Guid taskId, DateTime utcDate, CancellationToken cancellationToken = default);
}
