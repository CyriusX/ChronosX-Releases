using Microsoft.EntityFrameworkCore;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Repositories;

public sealed class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly TimeTrackDbContext _context;

    public PasswordResetTokenRepository(TimeTrackDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return await _context.PasswordResetTokens
            .Include(prt => prt.User)
            .FirstOrDefaultAsync(prt => prt.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<PasswordResetToken?> GetValidTokenByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.PasswordResetTokens
            .Where(prt => prt.UserId == userId && !prt.IsExpired && !prt.IsUsed)
            .OrderByDescending(prt => prt.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        await _context.PasswordResetTokens.AddAsync(token, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(PasswordResetToken token, CancellationToken cancellationToken = default)
    {
        _context.PasswordResetTokens.Update(token);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task InvalidateAllByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _context.PasswordResetTokens
            .Where(prt => prt.UserId == userId && !prt.IsUsed)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.MarkAsUsed();
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
