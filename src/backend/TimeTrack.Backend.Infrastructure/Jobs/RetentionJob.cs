using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

/// <summary>
/// Job de retenção de dados
/// Remove activity_sessions e idle_periods antigos baseado na política de retenção
/// Processa em batches de 1000 registros para evitar locks
/// </summary>
public sealed class RetentionJob : IRetentionJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IOrgPolicyRepository _policyRepository;
    private readonly ILogger<RetentionJob> _logger;

    private const int BatchSize = 1000;
    private const int DefaultRetentionDays = 90;

    public RetentionJob(
        TimeTrackDbContext context,
        IOrgPolicyRepository policyRepository,
        ILogger<RetentionJob> logger)
    {
        _context = context;
        _policyRepository = policyRepository;
        _logger = logger;
    }

    /// <summary>
    /// Executa a limpeza de dados antigos (método para Hangfire)
    /// </summary>
    public Task ExecuteAsync()
    {
        return ExecuteInternalAsync(CancellationToken.None);
    }

    private async Task ExecuteInternalAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting retention job at {Time}", DateTime.UtcNow);

        try
        {
            // Get all active organizations
            var orgs = await _context.Organizations
                .AsNoTracking()
                .IgnoreQueryFilters()
                .Where(o => o.Status == Domain.ValueObjects.OrgStatus.Active)
                .Select(o => new { o.Id })
                .ToListAsync(cancellationToken);

            var totalDeletedSessions = 0;
            var totalDeletedIdlePeriods = 0;

            foreach (var org in orgs)
            {
                var deleted = await ProcessOrganizationRetentionAsync(org.Id, cancellationToken);
                totalDeletedSessions += deleted.sessions;
                totalDeletedIdlePeriods += deleted.idlePeriods;
            }

            _logger.LogInformation(
                "Retention job completed. Deleted {Sessions} activity sessions and {IdlePeriods} idle periods",
                totalDeletedSessions,
                totalDeletedIdlePeriods);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing retention job");
            throw;
        }
    }

    private async Task<(int sessions, int idlePeriods)> ProcessOrganizationRetentionAsync(
        Guid orgId,
        CancellationToken cancellationToken)
    {
        // Get retention policy for organization
        var policy = await _policyRepository.GetByOrgIdAsync(orgId, cancellationToken);
        var retentionDays = policy?.RetentionDays ?? DefaultRetentionDays;
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);

        _logger.LogInformation(
            "Processing retention for org {OrgId} with cutoff date {CutoffDate} ({RetentionDays} days)",
            orgId,
            cutoffDate,
            retentionDays);

        var deletedSessions = await DeleteActivitySessionsBatchAsync(orgId, cutoffDate, cancellationToken);
        var deletedIdlePeriods = await DeleteIdlePeriodsBatchAsync(orgId, cutoffDate, cancellationToken);

        return (deletedSessions, deletedIdlePeriods);
    }

    private async Task<int> DeleteActivitySessionsBatchAsync(
        Guid orgId,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        var totalDeleted = 0;

        while (true)
        {
            // Fetch batch (compatible with InMemory provider)
            var batch = await _context.ActivitySessions
                .IgnoreQueryFilters()
                .Where(a => a.OrgId == orgId && a.StartedAt < cutoffDate)
                .OrderBy(a => a.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            _context.ActivitySessions.RemoveRange(batch);
            await _context.SaveChangesAsync(cancellationToken);

            totalDeleted += batch.Count;

            if (batch.Count < BatchSize)
                break;

            _logger.LogInformation(
                "Deleted {Count} activity sessions batch for org {OrgId}",
                batch.Count,
                orgId);

            await Task.Delay(100, cancellationToken);
        }

        return totalDeleted;
    }

    private async Task<int> DeleteIdlePeriodsBatchAsync(
        Guid orgId,
        DateTime cutoffDate,
        CancellationToken cancellationToken)
    {
        var totalDeleted = 0;

        while (true)
        {
            // Fetch batch (compatible with InMemory provider)
            var batch = await _context.IdlePeriods
                .IgnoreQueryFilters()
                .Where(i => i.OrgId == orgId && i.StartedAt < cutoffDate)
                .OrderBy(i => i.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            if (batch.Count == 0)
                break;

            _context.IdlePeriods.RemoveRange(batch);
            await _context.SaveChangesAsync(cancellationToken);

            totalDeleted += batch.Count;

            if (batch.Count < BatchSize)
                break;

            _logger.LogInformation(
                "Deleted {Count} idle periods batch for org {OrgId}",
                batch.Count,
                orgId);

            await Task.Delay(100, cancellationToken);
        }

        return totalDeleted;
    }
}
