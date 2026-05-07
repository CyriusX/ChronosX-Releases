using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Infrastructure.Jobs.Interfaces;
using TimeTrack.Backend.Infrastructure.Persistence;

namespace TimeTrack.Backend.Infrastructure.Jobs;

public sealed class UsageCounterRefreshJob : IUsageCounterRefreshJob
{
    private readonly TimeTrackDbContext _context;
    private readonly IOrgUsageRecordRepository _usageRepository;
    private readonly ILogger<UsageCounterRefreshJob> _logger;

    public UsageCounterRefreshJob(
        TimeTrackDbContext context,
        IOrgUsageRecordRepository usageRepository,
        ILogger<UsageCounterRefreshJob> logger)
    {
        _context = context;
        _usageRepository = usageRepository;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("UsageCounterRefreshJob: refreshing usage counters");

        var orgs = await _context.Organizations
            .IgnoreQueryFilters()
            .Where(o => o.Status == Domain.ValueObjects.OrgStatus.Active)
            .Select(o => o.Id)
            .ToListAsync();

        foreach (var orgId in orgs)
        {
            var activeUsers = await _context.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.OrgId == orgId && u.Status == Domain.ValueObjects.UserStatus.Active);

            var activeDevices = await _context.Devices
                .IgnoreQueryFilters()
                .CountAsync(d => d.OrgId == orgId && d.Status == Domain.ValueObjects.DeviceStatus.Active);

            var record = await _usageRepository.GetOrCreateAsync(orgId);
            record.UpdateCounts(activeUsers, activeDevices);
            await _usageRepository.UpdateAsync(record);
        }

        _logger.LogInformation("UsageCounterRefreshJob: refreshed counters for {Count} organizations", orgs.Count);
    }
}
