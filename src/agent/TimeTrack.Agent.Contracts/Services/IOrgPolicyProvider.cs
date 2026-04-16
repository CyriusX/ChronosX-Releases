namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Provides organization-level policies to the agent with local caching.
/// </summary>
public interface IOrgPolicyProvider
{
    Task<int?> GetIdleThresholdSecondsAsync(CancellationToken cancellationToken = default);
    Task RefreshAsync(bool force = false, CancellationToken cancellationToken = default);
}

