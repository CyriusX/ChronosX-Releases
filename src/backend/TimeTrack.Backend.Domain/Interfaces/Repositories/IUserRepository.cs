using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByIdUnfilteredAsync(Guid id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailUnfilteredAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailWithOrgAsync(string email, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsInOrgAsync(string email, Guid orgId, CancellationToken cancellationToken = default);
    Task<int> CountByOrgIdAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task AddAsync(User user, CancellationToken cancellationToken = default);
    Task UpdateAsync(User user, CancellationToken cancellationToken = default);
}
