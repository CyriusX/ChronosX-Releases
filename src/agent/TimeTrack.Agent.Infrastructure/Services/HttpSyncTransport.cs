using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação HTTP do transporte de sync
/// </summary>
public sealed class HttpSyncTransport : ISyncTransport, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly SyncSettings _settings;
    private readonly ILogger<HttpSyncTransport> _logger;
    private readonly ITokenStore? _tokenStore;
    private readonly JsonSerializerOptions _jsonOptions;

    public HttpSyncTransport(
        HttpClient httpClient,
        SyncSettings settings,
        ILogger<HttpSyncTransport> logger,
        ITokenStore? tokenStore = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenStore = tokenStore;

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };

        ConfigureHttpClient();
    }

    private void ConfigureHttpClient()
    {
        _httpClient.BaseAddress = new Uri(_settings.BackendUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.HttpTimeoutSeconds);

        // Static auth token from config (fallback)
        if (!string.IsNullOrWhiteSpace(_settings.AuthToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AuthToken);
        }
    }

    /// <summary>
    /// Ensures valid JWT token before making requests
    /// </summary>
    private async Task<bool> EnsureValidTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore is null)
            return true; // No token store, use static token from config

        // Check if token needs refresh
        if (_tokenStore.IsJwtExpiringSoon(5))
        {
            _logger.LogInformation("JWT expiring soon, attempting refresh");
            var refreshed = await _tokenStore.RefreshAsync(cancellationToken);

            if (!refreshed)
            {
                _logger.LogError("Failed to refresh token, user may be deactivated");
                return false;
            }
        }

        // Get current JWT and set authorization header
        var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
        if (!string.IsNullOrEmpty(jwt))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);
            return true;
        }

        _logger.LogWarning("No JWT available");
        return false;
    }

    /// <inheritdoc />
    public async Task<SyncResult> SendActivitySessionsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        if (!itemList.Any())
        {
            return SyncResult.Success(0, 0, Array.Empty<Guid>());
        }

        try
        {
            // Ensure valid token before request
            if (!await EnsureValidTokenAsync(cancellationToken))
            {
                return SyncResult.Failure("Authentication failed - user may be deactivated", 401);
            }

            var payload = BuildActivitySessionsPayload(itemList);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/ingest/activity-sessions",
                content,
                cancellationToken);

            // Handle 401 - try refresh and retry once
            if (response.StatusCode == HttpStatusCode.Unauthorized && _tokenStore is not null)
            {
                _logger.LogWarning("Received 401, attempting token refresh and retry");

                if (await _tokenStore.RefreshAsync(cancellationToken))
                {
                    var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(jwt))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", jwt);
                    }

                    // Retry request
                    var retryContent = new StringContent(
                        JsonSerializer.Serialize(payload, _jsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    response = await _httpClient.PostAsync(
                        "/api/v1/ingest/activity-sessions",
                        retryContent,
                        cancellationToken);
                }
            }

            return await ProcessResponseAsync(response, itemList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending activity sessions");
            return SyncResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout sending activity sessions");
            return SyncResult.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending activity sessions");
            return SyncResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<SyncResult> SendIdlePeriodsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default)
    {
        var itemList = items.ToList();
        if (!itemList.Any())
        {
            return SyncResult.Success(0, 0, Array.Empty<Guid>());
        }

        try
        {
            // Ensure valid token before request
            if (!await EnsureValidTokenAsync(cancellationToken))
            {
                return SyncResult.Failure("Authentication failed - user may be deactivated", 401);
            }

            var payload = BuildIdlePeriodsPayload(itemList);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/ingest/idle-periods",
                content,
                cancellationToken);

            // Handle 401 - try refresh and retry once
            if (response.StatusCode == HttpStatusCode.Unauthorized && _tokenStore is not null)
            {
                _logger.LogWarning("Received 401, attempting token refresh and retry");

                if (await _tokenStore.RefreshAsync(cancellationToken))
                {
                    var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
                    if (!string.IsNullOrEmpty(jwt))
                    {
                        _httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", jwt);
                    }

                    // Retry request
                    var retryContent = new StringContent(
                        JsonSerializer.Serialize(payload, _jsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    response = await _httpClient.PostAsync(
                        "/api/v1/ingest/idle-periods",
                        retryContent,
                        cancellationToken);
                }
            }

            return await ProcessResponseAsync(response, itemList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending idle periods");
            return SyncResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout sending idle periods");
            return SyncResult.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending idle periods");
            return SyncResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/health", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Health check failed");
            return false;
        }
    }

    private object BuildActivitySessionsPayload(IEnumerable<OutboxItem> items)
    {
        var itemList = items.ToList();
        var activityItems = new List<object>(itemList.Count);

        foreach (var item in itemList)
        {
            var payload = JsonSerializer.Deserialize<ActivitySessionPayload>(
                item.PayloadJson, _jsonOptions);

            if (payload != null)
            {
                activityItems.Add(new
                {
                    Id = item.EntityId,
                    payload.ProcessName,
                    payload.WindowTitle,
                    payload.AppCategory,
                    payload.StartedAt,
                    payload.EndedAt,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = activityItems };
    }

    private object BuildIdlePeriodsPayload(IEnumerable<OutboxItem> items)
    {
        var itemList = items.ToList();
        var idleItems = new List<object>(itemList.Count);

        foreach (var item in itemList)
        {
            var payload = JsonSerializer.Deserialize<IdlePeriodPayload>(
                item.PayloadJson, _jsonOptions);

            if (payload != null)
            {
                idleItems.Add(new
                {
                    Id = item.EntityId,
                    payload.StartedAt,
                    payload.EndedAt,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = idleItems };
    }

    private async Task<SyncResult> ProcessResponseAsync(
        HttpResponseMessage response,
        List<OutboxItem> items)
    {
        var statusCode = (int)response.StatusCode;

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<IngestResponseDto>(
                    content, _jsonOptions);

                if (result != null)
                {
                    _logger.LogInformation(
                        "Batch processed: {Processed} items, {Duplicates} duplicates",
                        result.Processed, result.Duplicates);

                    return SyncResult.Success(
                        result.Processed,
                        result.Duplicates,
                        items.Select(i => i.Id).ToList());
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse success response");
            }

            // Assume success even if we can't parse the response
            return SyncResult.Success(
                items.Count,
                0,
                items.Select(i => i.Id).ToList());
        }

        var errorContent = await response.Content.ReadAsStringAsync();
        _logger.LogWarning(
            "Batch rejected: StatusCode={StatusCode}, Error={Error}",
            statusCode,
            errorContent);

        return SyncResult.Failure(
            $"Server returned {statusCode}: {errorContent}",
            statusCode);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    #region DTOs

    private sealed class ActivitySessionPayload
    {
        public string ProcessName { get; set; } = string.Empty;
        public string? WindowTitle { get; set; }
        public string? AppCategory { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
    }

    private sealed class IdlePeriodPayload
    {
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
    }

    private sealed class IngestResponseDto
    {
        public int Processed { get; set; }
        public int Duplicates { get; set; }
    }

    #endregion
}
