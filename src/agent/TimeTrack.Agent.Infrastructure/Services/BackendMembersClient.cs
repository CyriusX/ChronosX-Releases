using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Fetches member summary from the backend API.
/// This is intentionally "best effort": failures should never break the agent UI.
/// </summary>
public sealed class BackendMembersClient : IBackendMembersClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<BackendMembersClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static string GetLocalIanaTimezone()
    {
        var local = TimeZoneInfo.Local;
        if (local.HasIanaId) return local.Id;
        return TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var ianaId) ? ianaId : "UTC";
    }

    public BackendMembersClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<BackendMembersClient> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<MemberSummaryResult?> GetMySummaryAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_tokenStore.IsJwtExpiringSoon(withinMinutes: 2))
            {
                await _tokenStore.RefreshAsync(cancellationToken);
            }

            var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
            if (string.IsNullOrEmpty(jwt))
            {
                _logger.LogWarning("[BackendMembers] No JWT available, skipping fetch");
                return null;
            }

            var timezone = Uri.EscapeDataString(GetLocalIanaTimezone());
            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/me/summary?timezone={timezone}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("[BackendMembers] Got 401, refreshing token...");
                var refreshed = await _tokenStore.RefreshAsync(cancellationToken);
                if (refreshed)
                {
                    jwt = await _tokenStore.GetJwtAsync(cancellationToken);
                    var retry = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/auth/me/summary?timezone={timezone}");
                    retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                    response = await _httpClient.SendAsync(retry, cancellationToken);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[BackendMembers] Failed: HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto = JsonSerializer.Deserialize<MemberSummaryDto>(json, JsonOptions);
            if (dto == null) return null;

            return new MemberSummaryResult
            {
                TopProjects = dto.TopProjects?.Select(p => new TopProjectSummaryItem
                {
                    Name = p.Name ?? string.Empty,
                    Duration = p.Duration,
                    Percentage = p.Percentage
                }).ToList() ?? []
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BackendMembers] Exception: {Message}", ex.Message);
            return null;
        }
    }

    private sealed class MemberSummaryDto
    {
        public List<TopProjectDto>? TopProjects { get; set; }
    }

    private sealed class TopProjectDto
    {
        public string? Name { get; set; }
        public long Duration { get; set; }
        public double Percentage { get; set; }
    }
}

