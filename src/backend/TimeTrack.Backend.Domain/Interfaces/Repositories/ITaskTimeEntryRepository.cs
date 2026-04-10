using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface ITaskTimeEntryRepository
{
    Task<TaskTimeEntry?> GetOpenForUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskTimeEntry>> ListForUserOnDateAsync(Guid userId, DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskTimeEntry>> ListForUserInRangeAsync(Guid userId, DateTime startUtc, DateTime endUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskTimeEntry>> ListForTaskAsync(Guid taskId, CancellationToken cancellationToken = default);
    Task AddAsync(TaskTimeEntry entry, CancellationToken cancellationToken = default);
    Task UpdateAsync(TaskTimeEntry entry, CancellationToken cancellationToken = default);
}
