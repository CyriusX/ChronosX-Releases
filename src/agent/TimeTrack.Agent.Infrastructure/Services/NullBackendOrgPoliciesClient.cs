using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// No-op implementation for local/test mode (no backend).
/// </summary>
public sealed class NullBackendOrgPoliciesClient : IBackendOrgPoliciesClient
{
    public Task<OrgPolicyResult?> GetOrgPolicyAsync(Guid orgId, CancellationToken cancellationToken = default)
        => Task.FromResult<OrgPolicyResult?>(null);
}

