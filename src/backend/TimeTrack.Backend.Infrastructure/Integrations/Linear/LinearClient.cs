using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Integrations.Linear;

namespace TimeTrack.Backend.Infrastructure.Integrations.Linear;

/// <summary>
/// HttpClient + System.Text.Json-based Linear GraphQL client.
/// There is no official Linear .NET SDK, and GraphQL is just POST+JSON,
/// so we hand-write the queries and deserialize into the DTOs in the
/// Application layer.
/// </summary>
public sealed class LinearClient : ILinearClient
{
    private const string GraphQlPath = ""; // BaseAddress already points at the /graphql endpoint

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly ILogger<LinearClient> _logger;

    public LinearClient(HttpClient http, ILogger<LinearClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<LinearViewer> GetViewerAsync(string apiKey, CancellationToken ct = default)
    {
        const string query = "query { viewer { id name email } }";
        var resp = await PostAsync<ViewerResponse>(apiKey, query, variables: null, ct);
        var v = resp.Viewer
            ?? throw new LinearApiException("Linear viewer query returned no data");
        return new LinearViewer(v.Id, v.Name, v.Email);
    }

    public async Task<IReadOnlyList<LinearProject>> GetProjectsForViewerAsync(string apiKey, CancellationToken ct = default)
    {
        const string query = @"
            query {
              projects(first: 100) {
                nodes { id name color state description }
              }
            }";
        var resp = await PostAsync<ProjectsResponse>(apiKey, query, variables: null, ct);
        var nodes = resp.Projects?.Nodes ?? new List<ProjectNode>();
        return nodes
            .Select(n => new LinearProject(n.Id, n.Name, n.Color, n.State, n.Description))
            .ToList();
    }

    public async Task<IReadOnlyList<LinearIssue>> GetMyIssuesAsync(string apiKey, CancellationToken ct = default)
    {
        const string query = @"
            query {
              issues(
                filter: { assignee: { isMe: { eq: true } } }
                first: 100
              ) {
                nodes {
                  id
                  identifier
                  title
                  description
                  priority
                  dueDate
                  url
                  state { id name type }
                  project { id name }
                  team { id key }
                }
              }
            }";
        var resp = await PostAsync<IssuesResponse>(apiKey, query, variables: null, ct);
        var nodes = resp.Issues?.Nodes ?? new List<IssueNode>();

        return nodes.Select(n => new LinearIssue(
            n.Id,
            n.Identifier,
            n.Title,
            n.Description,
            n.Priority,
            ParseDueDate(n.DueDate),
            n.Url,
            new LinearIssueState(n.State.Id, n.State.Name, n.State.Type),
            n.Project is null ? null : new LinearIssueProject(n.Project.Id, n.Project.Name),
            new LinearIssueTeam(n.Team.Id, n.Team.Key)
        )).ToList();
    }

    public async Task<IReadOnlyList<LinearWorkflowState>> GetTeamStatesAsync(string apiKey, string teamId, CancellationToken ct = default)
    {
        const string query = @"
            query GetTeamStates($teamId: String!) {
              team(id: $teamId) {
                states { nodes { id name type position } }
              }
            }";
        var resp = await PostAsync<TeamStatesResponse>(apiKey, query, new { teamId }, ct);
        var nodes = resp.Team?.States?.Nodes ?? new List<WorkflowStateNode>();
        return nodes
            .Select(n => new LinearWorkflowState(n.Id, n.Name, n.Type, n.Position))
            .ToList();
    }

    public async Task<bool> UpdateIssueStateAsync(string apiKey, string issueId, string stateId, CancellationToken ct = default)
    {
        const string mutation = @"
            mutation UpdateIssueState($id: String!, $stateId: String!) {
              issueUpdate(id: $id, input: { stateId: $stateId }) {
                success
              }
            }";
        var resp = await PostAsync<IssueUpdateResponse>(apiKey, mutation, new { id = issueId, stateId }, ct);
        return resp.IssueUpdate?.Success ?? false;
    }

    public async Task<LinearOAuthTokenResponse> ExchangeCodeForTokenAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        var clientId = Environment.GetEnvironmentVariable("LINEAR_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("LINEAR_CLIENT_SECRET") ?? "";

        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linear.app/oauth/token")
        {
            Content = new FormUrlEncodedContent(payload)
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LinearApiException("Network error exchanging Linear OAuth code", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Linear OAuth token exchange failed {Status}: {Body}", (int)response.StatusCode, Truncate(body, 500));
            throw new LinearApiException($"Linear OAuth token exchange returned {(int)response.StatusCode}");
        }

        try
        {
            var tokenData = JsonSerializer.Deserialize<OAuthTokenResponseBody>(body, JsonOptions);
            if (tokenData is null || string.IsNullOrEmpty(tokenData.AccessToken))
                throw new LinearApiException("Linear OAuth token response missing access_token");

            return new LinearOAuthTokenResponse(
                tokenData.AccessToken,
                tokenData.RefreshToken ?? string.Empty,
                tokenData.ExpiresIn,
                tokenData.TokenType ?? "Bearer");
        }
        catch (JsonException ex)
        {
            throw new LinearApiException("Malformed Linear OAuth token response", ex);
        }
    }

    public async Task<LinearOAuthTokenResponse> RefreshAccessTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        var clientId = Environment.GetEnvironmentVariable("LINEAR_CLIENT_ID") ?? "";
        var clientSecret = Environment.GetEnvironmentVariable("LINEAR_CLIENT_SECRET") ?? "";

        var payload = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.linear.app/oauth/token")
        {
            Content = new FormUrlEncodedContent(payload)
        };

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new LinearApiException("Network error refreshing Linear OAuth token", ex);
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Linear OAuth token refresh failed {Status}: {Body}", (int)response.StatusCode, Truncate(body, 500));
            throw new LinearApiException($"Linear OAuth token refresh returned {(int)response.StatusCode}");
        }

        try
        {
            var tokenData = JsonSerializer.Deserialize<OAuthTokenResponseBody>(body, JsonOptions);
            if (tokenData is null || string.IsNullOrEmpty(tokenData.AccessToken))
                throw new LinearApiException("Linear OAuth refresh response missing access_token");

            return new LinearOAuthTokenResponse(
                tokenData.AccessToken,
                tokenData.RefreshToken ?? refreshToken,
                tokenData.ExpiresIn,
                tokenData.TokenType ?? "Bearer");
        }
        catch (JsonException ex)
        {
            throw new LinearApiException("Malformed Linear OAuth refresh response", ex);
        }
    }

    // ── Internals ──────────────────────────────────────────────────────────

    private async Task<T> PostAsync<T>(string apiKey, string query, object? variables, CancellationToken ct) where T : class
    {
        var body = new { query, variables };
        using var request = new HttpRequestMessage(HttpMethod.Post, GraphQlPath)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
        // Linear personal API keys are sent as-is in the Authorization header (no "Bearer").
        request.Headers.Authorization = new AuthenticationHeaderValue(apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            throw new LinearApiException("Network error contacting Linear", ex);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new LinearUnauthorizedException("Linear rejected the API key (401)");

        var payload = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Linear API non-2xx {Status}: {Body}", (int)response.StatusCode, Truncate(payload, 500));
            throw new LinearApiException($"Linear API returned {(int)response.StatusCode}");
        }

        GraphQlEnvelope<T>? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<GraphQlEnvelope<T>>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Malformed Linear response: {Body}", Truncate(payload, 500));
            throw new LinearApiException("Malformed JSON from Linear", ex);
        }

        if (envelope?.Errors is { Count: > 0 } errors)
        {
            var message = string.Join("; ", errors.Select(e => e.Message));
            if (errors.Any(e => string.Equals(e.Extensions?.Code, "AUTHENTICATION_ERROR", StringComparison.OrdinalIgnoreCase)))
                throw new LinearUnauthorizedException($"Linear auth failure: {message}");
            throw new LinearApiException($"Linear GraphQL errors: {message}");
        }

        if (envelope?.Data is null)
            throw new LinearApiException("Linear response contained no data");

        return envelope.Data;
    }

    private static DateTime? ParseDueDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "…";

    // ── Raw response shapes (private; mapped to public DTOs above) ─────────

    private sealed class GraphQlEnvelope<T>
    {
        [JsonPropertyName("data")] public T? Data { get; set; }
        [JsonPropertyName("errors")] public List<GraphQlError>? Errors { get; set; }
    }

    private sealed class GraphQlError
    {
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
        [JsonPropertyName("extensions")] public GraphQlErrorExtensions? Extensions { get; set; }
    }

    private sealed class GraphQlErrorExtensions
    {
        [JsonPropertyName("code")] public string? Code { get; set; }
    }

    private sealed class ViewerResponse
    {
        [JsonPropertyName("viewer")] public ViewerNode? Viewer { get; set; }
    }

    private sealed class ViewerNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("email")] public string? Email { get; set; }
    }

    private sealed class ProjectsResponse
    {
        [JsonPropertyName("projects")] public NodeList<ProjectNode>? Projects { get; set; }
    }

    private sealed class ProjectNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("color")] public string? Color { get; set; }
        [JsonPropertyName("state")] public string? State { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
    }

    private sealed class IssuesResponse
    {
        [JsonPropertyName("issues")] public NodeList<IssueNode>? Issues { get; set; }
    }

    private sealed class IssueNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("identifier")] public string Identifier { get; set; } = string.Empty;
        [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("priority")] public int Priority { get; set; }
        [JsonPropertyName("dueDate")] public string? DueDate { get; set; }
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("state")] public StateNode State { get; set; } = new();
        [JsonPropertyName("project")] public ProjectRefNode? Project { get; set; }
        [JsonPropertyName("team")] public TeamRefNode Team { get; set; } = new();
    }

    private sealed class StateNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    }

    private sealed class ProjectRefNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    }

    private sealed class TeamRefNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("key")] public string? Key { get; set; }
    }

    private sealed class TeamStatesResponse
    {
        [JsonPropertyName("team")] public TeamStatesNode? Team { get; set; }
    }

    private sealed class TeamStatesNode
    {
        [JsonPropertyName("states")] public NodeList<WorkflowStateNode>? States { get; set; }
    }

    private sealed class WorkflowStateNode
    {
        [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("position")] public double Position { get; set; }
    }

    private sealed class IssueUpdateResponse
    {
        [JsonPropertyName("issueUpdate")] public IssueUpdateNode? IssueUpdate { get; set; }
    }

    private sealed class IssueUpdateNode
    {
        [JsonPropertyName("success")] public bool Success { get; set; }
    }

    private sealed class NodeList<T>
    {
        [JsonPropertyName("nodes")] public List<T> Nodes { get; set; } = new();
    }

    private sealed class OAuthTokenResponseBody
    {
        [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    }
}
