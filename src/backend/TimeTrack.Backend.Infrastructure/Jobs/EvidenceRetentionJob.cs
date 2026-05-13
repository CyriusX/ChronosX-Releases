using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.Interfaces.Services;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job de retenção de evidências — remove screenshots expirados por org.
/// Deleta metadata do DB e arquivo do Media Service.
/// Roda diariamente às 03:00 UTC.
/// </summary>
public sealed class EvidenceRetentionJob : IEvidenceRetentionJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IOrgPolicyRepository _policyRepository;
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly IOrganizationRepository _orgRepository;
    private readonly IMediaServiceClient _mediaService;
    private readonly ILogger<EvidenceRetentionJob> _logger;

    private const int BatchSize = 500;
    private const int DefaultEvidenceRetentionDays = 30;

    public EvidenceRetentionJob(
        TimeTrackDbContext context,
        IOrgPolicyRepository policyRepository,
        IEvidenceItemRepository evidenceRepository,
        IOrganizationRepository orgRepository,
        IMediaServiceClient mediaService,
        ILogger<EvidenceRetentionJob> logger)
    {
        _context = context;
        _policyRepository = policyRepository;
        _evidenceRepository = evidenceRepository;
        _orgRepository = orgRepository;
        _mediaService = mediaService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting evidence retention job at {Time}", DateTime.UtcNow);

        try
        {
            var orgs = await _context.Organizations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.Status == Domain.ValueObjects.OrgStatus.Active)
                .Select(o => new { o.Id })
                .ToListAsync();

            var totalDeleted = 0;

            foreach (var org in orgs)
            {
                var deleted = await ProcessOrgRetentionAsync(org.Id);
                totalDeleted += deleted;
            }

            _logger.LogInformation("Evidence retention job completed. Deleted {Count} evidence items", totalDeleted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing evidence retention job");
            throw;
        }
    }

    private async Task<int> ProcessOrgRetentionAsync(Guid orgId)
    {
        var policy = await _policyRepository.GetByOrgIdAsync(orgId);
        var retentionDays = policy?.EvidenceRetentionDays ?? DefaultEvidenceRetentionDays;
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation(
            "Processing evidence retention for org {OrgId}: cutoff={Cutoff}, retention={Days}d",
            orgId, cutoffDate, retentionDays);

        var totalDeleted = 0;

        while (true)
        {
            var expired = await _evidenceRepository.GetExpiredAsync(orgId, cutoffDate, BatchSize, CancellationToken.None);
            if (expired.Count == 0) break;

            foreach (var item in expired)
            {
                // Delete from Media Service if external ID exists
                if (!string.IsNullOrEmpty(item.ExternalMediaId))
                {
                    try
                    {
                        await _mediaService.DeleteMediaAsync(item.ExternalMediaId, CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "Failed to delete media {MediaId} for evidence {EvidenceId}, continuing with DB cleanup",
                            item.ExternalMediaId, item.Id);
                    }
                }

                // Soft-deleted items older than 7 days → hard delete
                if (item.IsDeleted && item.DeletedAt.HasValue && item.DeletedAt.Value < DateTime.UtcNow.AddDays(-7))
                {
                    _context.EvidenceItems.Remove(item);
                }
                else
                {
                    item.SoftDelete();
                }
            }

            await _context.SaveChangesAsync();

            // Decrement org storage usage for hard-deleted items
            var hardDeletedBytes = expired
                .Where(i => i.IsDeleted && i.DeletedAt.HasValue && i.DeletedAt.Value < DateTime.UtcNow.AddDays(-7))
                .Sum(i => i.FileSizeBytes);
            if (hardDeletedBytes > 0)
            {
                try
                {
                    var org = await _orgRepository.GetByIdAsync(orgId, CancellationToken.None);
                    if (org != null)
                    {
                        org.DecrementStorageUsage(hardDeletedBytes);
                        await _orgRepository.UpdateAsync(org, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to decrement storage for org {OrgId}", orgId);
                }
            }

            totalDeleted += expired.Count;

            if (expired.Count < BatchSize) break;

            _logger.LogInformation("Deleted batch of {Count} evidence items for org {OrgId}", expired.Count, orgId);
            await Task.Delay(100);
        }

        return totalDeleted;
    }
}
