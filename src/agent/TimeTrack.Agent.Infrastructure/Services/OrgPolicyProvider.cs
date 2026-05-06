using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Fetches org policies from the backend and caches them in SQLite + memory.
/// Used by workers to enforce org-level idle threshold even for collaborators.
/// </summary>
public sealed class OrgPolicyProvider : IOrgPolicyProvider
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMinutes(15);

    private readonly ICurrentUserContext _userContext;
    private readonly IBackendOrgPoliciesClient _client;
    private readonly IOrgPolicyCacheRepository _cache;
    private readonly ILogger<OrgPolicyProvider> _logger;

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private Guid? _orgId;
    private OrgPolicyCacheEntry? _memoryEntry;
    private DateTime _lastRefreshAttemptUtc = DateTime.MinValue;

    public OrgPolicyProvider(
        ICurrentUserContext userContext,
        IBackendOrgPoliciesClient client,
        IOrgPolicyCacheRepository cache,
        ILogger<OrgPolicyProvider> logger)
    {
        _userContext = userContext;
        _client = client;
        _cache = cache;
        _logger = logger;
    }

    public async Task<int?> GetIdleThresholdSecondsAsync(CancellationToken cancellationToken = default)
    {
        var entry = await GetEffectiveCacheEntryAsync(cancellationToken);
        return entry?.IdleThresholdSeconds;
    }

    public async Task<int?> GetIdleJustificationPromptThresholdSecondsAsync(CancellationToken cancellationToken = default)
    {
        var entry = await GetEffectiveCacheEntryAsync(cancellationToken);
        return entry?.IdleJustificationPromptThresholdSeconds;
    }

    private async Task<OrgPolicyCacheEntry?> GetEffectiveCacheEntryAsync(CancellationToken cancellationToken)
    {
        if (!_userContext.OrgId.HasValue)
            return null;

        var currentOrgId = _userContext.OrgId.Value;

        // If user switched orgs, drop memory cache.
        if (_orgId != currentOrgId)
        {
            _orgId = currentOrgId;
            _memoryEntry = null;
            _lastRefreshAttemptUtc = DateTime.MinValue;
        }

        // Lazy-load from SQLite.
        if (_memoryEntry is null)
        {
            _memoryEntry = await _cache.GetAsync(currentOrgId, cancellationToken);
        }

        // Refresh if stale (best-effort).
        var isStale = _memoryEntry is null ||
            (DateTime.UtcNow - _memoryEntry.UpdatedAtUtc) > RefreshInterval;

        if (isStale && (DateTime.UtcNow - _lastRefreshAttemptUtc) > TimeSpan.FromSeconds(10))
        {
            _ = RefreshAsync(force: false, cancellationToken);
        }

        return _memoryEntry;
    }

    public async Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        if (!_userContext.OrgId.HasValue)
            return;

        var currentOrgId = _userContext.OrgId.Value;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            _lastRefreshAttemptUtc = DateTime.UtcNow;

            // Re-check staleness under lock.
            if (!force && _memoryEntry is not null && (DateTime.UtcNow - _memoryEntry.UpdatedAtUtc) <= RefreshInterval)
                return;

            var policy = await _client.GetOrgPolicyAsync(currentOrgId, cancellationToken);
            if (policy is null)
            {
                // Keep existing cache if backend is unreachable.
                _logger.LogDebug("Org policy refresh skipped/failed (no response). OrgId={OrgId}", currentOrgId);
                return;
            }

            var entry = new OrgPolicyCacheEntry
            {
                OrgId = policy.OrgId,
                IdleThresholdSeconds = policy.IdleThresholdSeconds,
                IdleJustificationPromptThresholdSeconds = policy.IdleJustificationPromptThresholdSeconds,
                Version = policy.Version,
                UpdatedAtUtc = policy.UpdatedAt?.ToUniversalTime() ?? DateTime.UtcNow
            };

            await _cache.UpsertAsync(entry, cancellationToken);
            _memoryEntry = entry;

            _logger.LogInformation(
                "Org policy refreshed. OrgId={OrgId}, IdleThreshold={Idle}s, IdleJustificationPromptThreshold={PromptThreshold}s, Version={Version}",
                entry.OrgId, entry.IdleThresholdSeconds, entry.IdleJustificationPromptThresholdSeconds, entry.Version);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Org policy refresh failed");
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
