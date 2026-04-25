using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// HTTP client for org policies endpoints. Handles JWT auth + single retry on 401.
/// </summary>
public sealed class BackendOrgPoliciesClient : IBackendOrgPoliciesClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<BackendOrgPoliciesClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public BackendOrgPoliciesClient(HttpClient httpClient, ITokenStore tokenStore, ILogger<BackendOrgPoliciesClient> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<OrgPolicyResult?> GetOrgPolicyAsync(Guid orgId, CancellationToken ct = default)
    {
        var response = await SendAsync(HttpMethod.Get, $"/api/v1/orgs/{orgId}/policies", null, ct);
        if (response is null) return null;
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[BackendOrgPolicies] GET org policy failed: {Status}", response.StatusCode);
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return null;

        var dto = JsonSerializer.Deserialize<OrgPolicyResponseDto>(json, JsonOptions);
        if (dto is null) return null;

        return new OrgPolicyResult
        {
            OrgId = dto.OrgId,
            Version = dto.Version,
            IdleThresholdSeconds = dto.IdleThresholdSeconds,
            IdleJustificationPromptThresholdSeconds = dto.IdleJustificationPromptThresholdSeconds,
            UpdatedAt = dto.UpdatedAt
        };
    }

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            if (_tokenStore.IsJwtExpiringSoon(2))
                await _tokenStore.RefreshAsync(ct);

            var jwt = await _tokenStore.GetJwtAsync(ct);
            if (string.IsNullOrEmpty(jwt))
                return null;

            var response = await SendOnceAsync(method, path, jwt, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                var refreshed = await _tokenStore.RefreshAsync(ct);
                if (!refreshed) return null;
                jwt = await _tokenStore.GetJwtAsync(ct);
                if (string.IsNullOrEmpty(jwt)) return null;
                response = await SendOnceAsync(method, path, jwt, ct);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackendOrgPolicies] {Method} {Path} threw", method, path);
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, string jwt, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return await _httpClient.SendAsync(request, ct);
    }

    private sealed class OrgPolicyResponseDto
    {
        public Guid OrgId { get; init; }
        public int Version { get; init; }
        public int IdleThresholdSeconds { get; init; }
        public int? IdleJustificationPromptThresholdSeconds { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
