namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Client for fetching member-centric summary data from the backend API.
/// Used by the agent to enrich the local dashboard summary with backend-derived
/// aggregations (e.g., top projects/tasks), without making the whole dashboard
/// depend on the cloud.
/// </summary>
public interface IBackendMembersClient
{
    /// <summary>
    /// Fetches the current user's "today" summary from:
    /// GET /api/v1/auth/me/summary?timezone={iana}
    /// </summary>
    Task<MemberSummaryResult?> GetMySummaryAsync(CancellationToken cancellationToken = default);
}

public sealed class MemberSummaryResult
{
    public List<TopProjectSummaryItem> TopProjects { get; init; } = [];
}

public sealed class TopProjectSummaryItem
{
    public string Name { get; init; } = string.Empty;
    public long Duration { get; init; }
    public double Percentage { get; init; }
}

