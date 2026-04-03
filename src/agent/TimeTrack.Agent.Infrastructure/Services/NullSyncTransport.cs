using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Null Object implementation of ISyncTransport for local testing.
/// Does not actually sync - just logs and returns success.
/// </summary>
public sealed class NullSyncTransport : ISyncTransport
{
    private readonly ILogger<NullSyncTransport> _logger;

    public NullSyncTransport(ILogger<NullSyncTransport> logger)
    {
        _logger = logger;
    }

    public Task<SyncResult> SendActivitySessionsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogDebug(
            "[NullSyncTransport] Pretending to sync {Count} activity sessions",
            itemList.Count);

        return Task.FromResult(SyncResult.Success(
            itemList.Count,
            0,
            itemList.Select(i => i.Id).ToList()));
    }

    public Task<SyncResult> SendIdlePeriodsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogDebug(
            "[NullSyncTransport] Pretending to sync {Count} idle periods",
            itemList.Count);

        return Task.FromResult(SyncResult.Success(
            itemList.Count,
            0,
            itemList.Select(i => i.Id).ToList()));
    }

    public Task<SyncResult> SendFocusSessionsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogDebug(
            "[NullSyncTransport] Pretending to sync {Count} focus sessions",
            itemList.Count);

        return Task.FromResult(SyncResult.Success(
            itemList.Count,
            0,
            itemList.Select(i => i.Id).ToList()));
    }

    public Task<SyncResult> SendMachineMetricsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        _logger.LogDebug(
            "[NullSyncTransport] Pretending to sync {Count} machine metrics",
            itemList.Count);

        return Task.FromResult(SyncResult.Success(
            itemList.Count,
            0,
            itemList.Select(i => i.Id).ToList()));
    }

    public Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("[NullSyncTransport] Health check - returning true (local mode)");
        return Task.FromResult(true);
    }
}
