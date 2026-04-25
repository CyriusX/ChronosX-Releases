namespace TimeTrack.Agent.Contracts.Repositories;

/// <summary>
/// Local SQLite cache for organization policies used by the agent.
/// Currently stores the org-level idle threshold so collaborators cannot
/// override the organization's policy locally.
/// </summary>
public interface IOrgPolicyCacheRepository
{
    Task<OrgPolicyCacheEntry?> GetAsync(Guid orgId, CancellationToken cancellationToken = default);
    Task UpsertAsync(OrgPolicyCacheEntry entry, CancellationToken cancellationToken = default);
}

public sealed class OrgPolicyCacheEntry
{
    public Guid OrgId { get; init; }
    public int IdleThresholdSeconds { get; init; }
    public int? IdleJustificationPromptThresholdSeconds { get; init; }
    public int Version { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
