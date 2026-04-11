namespace TimeTrack.Backend.Application.Integrations.Linear;

/// <summary>
/// Thin wrapper around Linear's GraphQL API. All methods take the raw
/// Personal API Key so the infrastructure layer does not need to touch
/// the encrypted UserIntegration row directly — decryption is a pure
/// concern of the Application layer sync orchestrator.
/// </summary>
public interface ILinearClient
{
    /// <summary>GET the user behind the token. Throws on 401.</summary>
    Task<LinearViewer> GetViewerAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>List all projects visible to the user.</summary>
    Task<IReadOnlyList<LinearProject>> GetProjectsForViewerAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>List open issues assigned to the viewer. Pagination handled internally.</summary>
    Task<IReadOnlyList<LinearIssue>> GetMyIssuesAsync(string apiKey, CancellationToken cancellationToken = default);

    /// <summary>Fetch all workflow states for a given team (used for status push mapping).</summary>
    Task<IReadOnlyList<LinearWorkflowState>> GetTeamStatesAsync(string apiKey, string teamId, CancellationToken cancellationToken = default);

    /// <summary>Issue a mutation to move a Linear issue to a specific workflow state.</summary>
    Task<bool> UpdateIssueStateAsync(string apiKey, string issueId, string stateId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thrown by <see cref="ILinearClient"/> when Linear rejects the token.
/// Callers should mark the UserIntegration as ErrorUnauthorized and prompt
/// the user to reconnect.
/// </summary>
public sealed class LinearUnauthorizedException : Exception
{
    public LinearUnauthorizedException(string message) : base(message) { }
}

/// <summary>
/// Thrown on any other Linear API failure — network, 5xx, rate limit, malformed response.
/// </summary>
public sealed class LinearApiException : Exception
{
    public LinearApiException(string message) : base(message) { }
    public LinearApiException(string message, Exception inner) : base(message, inner) { }
}
