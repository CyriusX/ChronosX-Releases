using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Services;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job de limpeza de arquivos órfãos no Media Service.
/// Lista objetos no bucket e remove os que não têm evidence_item correspondente.
/// Roda semanalmente (domingo às 02:00 UTC).
/// </summary>
public sealed class OrphanCleanupJob : IOrphanCleanupJob
{
    private readonly TimeTrackDbContext _context;
    private readonly ILogger<OrphanCleanupJob> _logger;

    public OrphanCleanupJob(
        TimeTrackDbContext context,
        ILogger<OrphanCleanupJob> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting orphan cleanup job at {Time}", DateTime.UtcNow);

        try
        {
            // Get all known storage keys from evidence_items
            var knownKeys = (await _context.Set<Domain.Entities.EvidenceItem>()
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(e => !e.IsDeleted)
                .Select(e => e.StorageKey)
                .ToListAsync())
                .Where(k => k != null)
                .ToHashSet();

            _logger.LogInformation("Found {Count} known storage keys in DB", knownKeys.Count);

            // Note: Full orphan detection requires listing objects from the Media Service.
            // The Media Service doesn't currently expose a "list all keys" endpoint.
            // For now, we focus on cleaning up soft-deleted items whose media should be removed.

            var softDeletedItems = await _context.Set<Domain.Entities.EvidenceItem>()
                .IgnoreQueryFilters()
                .Where(e => e.IsDeleted && e.DeletedAt != null && e.DeletedAt < DateTime.UtcNow.AddDays(-7))
                .ToListAsync();

            foreach (var item in softDeletedItems)
            {
                _logger.LogInformation(
                    "Hard deleting orphan evidence item {Id} (soft-deleted at {DeletedAt})",
                    item.Id, item.DeletedAt);

                _context.EvidenceItems.Remove(item);
            }

            var removed = await _context.SaveChangesAsync();
            _logger.LogInformation("Orphan cleanup completed. Hard-deleted {Count} evidence items", removed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing orphan cleanup job");
            throw;
        }
    }
}
