using TimeTrack.Backend.Domain.Entities;

namespace TimeTrack.Backend.Domain.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetValidTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task UpdateAsync(PasswordResetToken token, CancellationToken cancellationToken = default);
    Task InvalidateAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
