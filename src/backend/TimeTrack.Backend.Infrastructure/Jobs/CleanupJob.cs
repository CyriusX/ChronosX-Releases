using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job de limpeza de dados auxiliares
/// Remove chaves de idempotência expiradas (mais de 7 dias)
/// </summary>
public sealed class CleanupJob : ICleanupJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ILogger<CleanupJob> _logger;

    private const int BatchSize = 1000;
    private const int DefaultExpirationDays = 7;

    public CleanupJob(
        TimeTrackDbContext context,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ILogger<CleanupJob> logger)
    {
        _context = context;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting cleanup job at {Time}", DateTime.UtcNow);

        try
        {
            var totalDeleted = 0;

            while (true)
            {
                // Fetch expired keys in batch (compatible with InMemory provider)
                var expiredKeys = await _context.IdempotencyKeys
                    .IgnoreQueryFilters()
                    .Where(k => k.ExpiresAt < DateTime.UtcNow)
                    .OrderBy(k => k.Id)
                    .Take(BatchSize)
                    .ToListAsync();

                if (expiredKeys.Count == 0)
                    break;

                // Remove the batch
                _context.IdempotencyKeys.RemoveRange(expiredKeys);
                await _context.SaveChangesAsync();

                totalDeleted += expiredKeys.Count;

                if (expiredKeys.Count < BatchSize)
                    break;

                _logger.LogInformation("Deleted {Count} expired idempotency keys batch", expiredKeys.Count);

                // Small delay between batches
                await Task.Delay(100);
            }

            _logger.LogInformation(
                "Cleanup job completed. Deleted {Count} expired idempotency keys (older than {Days} days)",
                totalDeleted,
                DefaultExpirationDays);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing cleanup job");
            throw;
        }
    }
}
