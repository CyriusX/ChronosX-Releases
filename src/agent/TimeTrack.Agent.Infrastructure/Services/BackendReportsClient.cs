using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Fetches report data from the backend API.
/// Used to load past days' weekly history from the cloud.
/// </summary>
public sealed class BackendReportsClient : IBackendReportsClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<BackendReportsClient> _logger;

    // Cache past days' reports — they don't change, no need to re-fetch every 5s
    private readonly Dictionary<string, (DailyReportResult result, DateTime fetchedAt)> _cache = new();
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public BackendReportsClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<BackendReportsClient> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<DailyReportResult?> GetDailySummaryAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        var cacheKey = date.ToString("yyyy-MM-dd");

        // Return cached result if still valid
        if (_cache.TryGetValue(cacheKey, out var cached) && (DateTime.UtcNow - cached.fetchedAt) < CacheDuration)
        {
            return cached.result;
        }

        try
        {
            // Ensure token is fresh (refresh if expiring soon)
            if (_tokenStore.IsJwtExpiringSoon(withinMinutes: 2))
            {
                await _tokenStore.RefreshAsync(cancellationToken);
            }

            var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
            if (string.IsNullOrEmpty(jwt))
            {
                _logger.LogWarning("[BackendReports] No JWT available, skipping fetch");
                return null;
            }

            var dateStr = date.ToString("yyyy-MM-dd");
            _logger.LogInformation("[BackendReports] Fetching daily report for {Date}", dateStr);

            var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/reports/daily?date={dateStr}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            // Retry once with refreshed token on 401
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("[BackendReports] Got 401 for {Date}, refreshing token...", dateStr);
                var refreshed = await _tokenStore.RefreshAsync(cancellationToken);
                if (refreshed)
                {
                    jwt = await _tokenStore.GetJwtAsync(cancellationToken);
                    var retryRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/reports/daily?date={dateStr}");
                    retryRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
                    response = await _httpClient.SendAsync(retryRequest, cancellationToken);
                }
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("[BackendReports] Failed for {Date}: HTTP {StatusCode}", dateStr, response.StatusCode);
                return null;
            }

            _logger.LogInformation("[BackendReports] Success for {Date}", dateStr);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var dto = JsonSerializer.Deserialize<DailySummaryDto>(json, JsonOptions);
            if (dto == null) return null;

            var result = new DailyReportResult
            {
                TotalActiveSeconds = dto.TotalActiveSeconds,
                TotalIdleSeconds = dto.TotalIdleSeconds,
                Apps = dto.Apps?.Select(a => new DailyReportApp
                {
                    DisplayName = a.DisplayName ?? string.Empty,
                    TotalSeconds = a.TotalSeconds,
                    SessionCount = a.SessionCount
                }).ToList() ?? []
            };

            // Cache the result — past days' data doesn't change
            _cache[cacheKey] = (result, DateTime.UtcNow);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BackendReports] Exception fetching {Date}: {Message}", date.ToString("yyyy-MM-dd"), ex.Message);
            return null;
        }
    }

    // DTOs matching the backend DailySummaryResponse JSON
    private sealed class DailySummaryDto
    {
        public long TotalActiveSeconds { get; set; }
        public long TotalIdleSeconds { get; set; }
        public List<AppDto>? Apps { get; set; }
    }

    private sealed class AppDto
    {
        public string? DisplayName { get; set; }
        public long TotalSeconds { get; set; }
        public int SessionCount { get; set; }
    }
}
