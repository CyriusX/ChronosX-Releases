namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Client for organization policies endpoints on the backend.
/// Used by the agent to enforce org-level policies (e.g. idle threshold).
/// </summary>
public interface IBackendOrgPoliciesClient
{
    Task<OrgPolicyResult?> GetOrgPolicyAsync(Guid orgId, CancellationToken cancellationToken = default);
}

public sealed class OrgPolicyResult
{
    public Guid OrgId { get; init; }
    public int Version { get; init; }
    public int IdleThresholdSeconds { get; init; }
    public int? IdleJustificationPromptThresholdSeconds { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
