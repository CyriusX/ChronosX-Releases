using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IStripeEventLogRepository
{
    Task<bool> HasBeenProcessedAsync(string stripeEventId, CancellationToken cancellationToken = default);
    Task AddAsync(StripeEventLog eventLog, CancellationToken cancellationToken = default);
}
