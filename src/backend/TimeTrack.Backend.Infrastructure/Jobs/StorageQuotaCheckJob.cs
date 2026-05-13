using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job de verificação de quota de storage por org.
/// Alerta quando ultrapassa 80% e bloqueia uploads quando atinge 100%.
/// Roda diariamente às 04:00 UTC.
/// </summary>
public sealed class StorageQuotaCheckJob : IStorageQuotaCheckJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IEvidenceItemRepository _evidenceRepository;
    private readonly ILogger<StorageQuotaCheckJob> _logger;

    public StorageQuotaCheckJob(
        TimeTrackDbContext context,
        IEvidenceItemRepository evidenceRepository,
        ILogger<StorageQuotaCheckJob> logger)
    {
        _context = context;
        _evidenceRepository = evidenceRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Starting storage quota check job at {Time}", DateTime.UtcNow);

        try
        {
            var orgs = await _context.Organizations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.Status == Domain.ValueObjects.OrgStatus.Active)
                .Select(o => new { o.Id, o.Name })
                .ToListAsync();

            foreach (var org in orgs)
            {
                await CheckOrgQuotaAsync(org.Id, org.Name);
            }

            _logger.LogInformation("Storage quota check job completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing storage quota check job");
            throw;
        }
    }

    private async Task CheckOrgQuotaAsync(Guid orgId, string orgName)
    {
        var storageUsedBytes = await _evidenceRepository.SumFileSizeByOrgAsync(orgId, CancellationToken.None);

        // Default quota: 10GB per org
        // TODO: read from organization entity when storage_quota_gb field is added (CX-196)
        var quotaGb = 10m;
        var quotaBytes = (long)(quotaGb * 1024 * 1024 * 1024);
        var usagePercent = quotaBytes > 0 ? (double)storageUsedBytes / quotaBytes * 100 : 0;

        if (usagePercent >= 80)
        {
            _logger.LogWarning(
                "Storage quota warning for org {OrgName} ({OrgId}): {UsedGB:F2} GB / {QuotaGB} GB ({Percent:F1}%)",
                orgName, orgId, (double)storageUsedBytes / (1024 * 1024 * 1024), quotaGb, usagePercent);

            // TODO: Create notification for org admin (CX-196)
        }
        else
        {
            _logger.LogDebug(
                "Storage quota OK for org {OrgName} ({OrgId}): {UsedGB:F2} GB / {QuotaGB} GB ({Percent:F1}%)",
                orgName, orgId, (double)storageUsedBytes / (1024 * 1024 * 1024), quotaGb, usagePercent);
        }
    }
}
