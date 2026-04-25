using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Infrastructure.Providers.Windows;

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

    public async Task<SyncResult> SendIdleJustificationsAsync(
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
            if (!await EnsureValidTokenAsync(cancellationToken))
            {
                return SyncResult.Failure("Authentication failed - user may be deactivated", 401);
            }

            var payload = BuildIdleJustificationsPayload(itemList);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/ingest/idle-justifications",
                content,
                cancellationToken);

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

                    var retryContent = new StringContent(
                        JsonSerializer.Serialize(payload, _jsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    response = await _httpClient.PostAsync(
                        "/api/v1/ingest/idle-justifications",
                        retryContent,
                        cancellationToken);
                }
            }

            return await ProcessResponseAsync(response, itemList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending idle justifications");
            return SyncResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout sending idle justifications");
            return SyncResult.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending idle justifications");
            return SyncResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<SyncResult> SendFocusSessionsAsync(
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

            var payload = BuildFocusSessionsPayload(itemList);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/ingest/focus-sessions",
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
                        "/api/v1/ingest/focus-sessions",
                        retryContent,
                        cancellationToken);
                }
            }

            return await ProcessResponseAsync(response, itemList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending focus sessions");
            return SyncResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout sending focus sessions");
            return SyncResult.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending focus sessions");
            return SyncResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<SyncResult> SendAgentEventsAsync(
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
            if (!await EnsureValidTokenAsync(cancellationToken))
            {
                return SyncResult.Failure("Authentication failed - user may be deactivated", 401);
            }

            var payload = BuildAgentEventsPayload(itemList);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/ingest/agent-events",
                content,
                cancellationToken);

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

                    var retryContent = new StringContent(
                        JsonSerializer.Serialize(payload, _jsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    response = await _httpClient.PostAsync(
                        "/api/v1/ingest/agent-events",
                        retryContent,
                        cancellationToken);
                }
            }

            return await ProcessResponseAsync(response, itemList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending agent events");
            return SyncResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout sending agent events");
            return SyncResult.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending agent events");
            return SyncResult.Failure($"Unexpected error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<SyncResult> SendMachineMetricsAsync(
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
            if (!await EnsureValidTokenAsync(cancellationToken))
            {
                return SyncResult.Failure("Authentication failed - user may be deactivated", 401);
            }

            var payload = BuildMachineMetricsPayload(itemList);
            var content = new StringContent(
                JsonSerializer.Serialize(payload, _jsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                "/api/v1/ingest/machine-metrics",
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

                    var retryContent = new StringContent(
                        JsonSerializer.Serialize(payload, _jsonOptions),
                        Encoding.UTF8,
                        "application/json");

                    response = await _httpClient.PostAsync(
                        "/api/v1/ingest/machine-metrics",
                        retryContent,
                        cancellationToken);
                }
            }

            return await ProcessResponseAsync(response, itemList);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error sending machine metrics");
            return SyncResult.Failure($"HTTP error: {ex.Message}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Timeout sending machine metrics");
            return SyncResult.Failure("Request timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending machine metrics");
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
                var processName = NormalizeBrowserProcessNameFromDisplayName(payload.DisplayName);

                // Use displayName as processName (required by backend)
                // Send both productivity and subcategory for alignment with Dashboard
                activityItems.Add(new
                {
                    Id = item.EntityId,
                    ProcessName = processName,
                    payload.WindowTitle,
                    payload.FilePath,
                    AppCategory = payload.CategoryProductivity,
                    AppSubcategory = payload.CategorySubcategory,
                    StartedAt = payload.StartUtc,
                    EndedAt = payload.EndUtc,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = activityItems };
    }

    private static readonly HashSet<string> KnownBrowserDisplayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Google Chrome", "Chrome",
        "Microsoft Edge", "Edge",
        "Mozilla Firefox", "Firefox",
        "Brave", "Opera", "Safari", "Arc",
        "Vivaldi", "Waterfox", "Chromium", "LibreWolf",
        "Internet Explorer"
    };

    private static string NormalizeBrowserProcessNameFromDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            return displayName;

        // Agent browser sessions use: "{Browser} - {Site/App}"
        var sep = " - ";
        var idx = displayName.IndexOf(sep, StringComparison.Ordinal);
        if (idx <= 0)
            return displayName;

        var browser = displayName[..idx].Trim();
        if (!KnownBrowserDisplayNames.Contains(browser))
            return displayName;

        var site = displayName[(idx + sep.Length)..].Trim();
        if (string.IsNullOrWhiteSpace(site))
            return displayName;

        var normalizedSite = BrowserUrlExtractor.NormalizeSiteName(site);
        if (string.IsNullOrWhiteSpace(normalizedSite))
            return displayName;

        return $"{browser} - {normalizedSite}";
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
                    StartedAt = payload.StartedAt,
                    EndedAt = payload.EndedAt,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = idleItems };
    }

    private object BuildIdleJustificationsPayload(IEnumerable<OutboxItem> items)
    {
        var itemList = items.ToList();
        var justificationItems = new List<object>(itemList.Count);

        foreach (var item in itemList)
        {
            var payload = JsonSerializer.Deserialize<IdleJustificationPayload>(
                item.PayloadJson, _jsonOptions);

            if (payload != null)
            {
                justificationItems.Add(new
                {
                    payload.IdlePeriodId,
                    payload.ReasonCode,
                    payload.Note,
                    payload.SubmittedAtUtc,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = justificationItems };
    }

    private object BuildFocusSessionsPayload(IEnumerable<OutboxItem> items)
    {
        var itemList = items.ToList();
        var focusItems = new List<object>(itemList.Count);

        foreach (var item in itemList)
        {
            var payload = JsonSerializer.Deserialize<FocusSessionPayload>(
                item.PayloadJson, _jsonOptions);

            if (payload != null)
            {
                focusItems.Add(new
                {
                    Id = item.EntityId,
                    StartedAt = payload.StartedAt,
                    EndedAt = payload.EndedAt,
                    PlannedDurationMinutes = payload.PlannedDurationMinutes,
                    ActualDurationMinutes = payload.ActualDurationMinutes,
                    Status = payload.Status,
                    FocusScore = payload.FocusScore,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = focusItems };
    }

    private object BuildAgentEventsPayload(IEnumerable<OutboxItem> items)
    {
        var itemList = items.ToList();
        var eventItems = new List<object>(itemList.Count);

        foreach (var item in itemList)
        {
            var payload = JsonSerializer.Deserialize<AgentEventPayload>(
                item.PayloadJson, _jsonOptions);

            if (payload != null)
            {
                eventItems.Add(new
                {
                    Id = item.EntityId,
                    payload.EventType,
                    payload.Category,
                    payload.Severity,
                    payload.Message,
                    payload.MetadataJson,
                    payload.Timestamp,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = eventItems };
    }

    private object BuildMachineMetricsPayload(IEnumerable<OutboxItem> items)
    {
        var itemList = items.ToList();
        var metricsItems = new List<object>(itemList.Count);

        foreach (var item in itemList)
        {
            var payload = JsonSerializer.Deserialize<MachineMetricsPayload>(
                item.PayloadJson, _jsonOptions);

            if (payload != null)
            {
                metricsItems.Add(new
                {
                    Id = item.EntityId,
                    payload.CpuPercent,
                    payload.MemoryUsedMb,
                    payload.MemoryTotalMb,
                    payload.DiskUsedGb,
                    payload.DiskTotalGb,
                    payload.SampledAt,
                    item.IdempotencyKey
                });
            }
        }

        return new { Items = metricsItems };
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
        public Guid Id { get; set; }
        public string ExePathHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string CategoryProductivity { get; set; } = string.Empty;
        public string CategorySubcategory { get; set; } = string.Empty;
        public string CategorySource { get; set; } = string.Empty;
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public string? WindowHash { get; set; }
        public string? WindowTitle { get; set; }
        public string? FilePath { get; set; }
    }

    private sealed class IdlePeriodPayload
    {
        public Guid Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public int ThresholdSeconds { get; set; }
        public bool IsSystemDetected { get; set; }
    }

    private sealed class IdleJustificationPayload
    {
        public Guid IdlePeriodId { get; set; }
        public string ReasonCode { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime SubmittedAtUtc { get; set; }
    }

    private sealed class FocusSessionPayload
    {
        public Guid Id { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? EndedAt { get; set; }
        public int PlannedDurationMinutes { get; set; }
        public int? ActualDurationMinutes { get; set; }
        public string Status { get; set; } = "InProgress";
        public int? FocusScore { get; set; }
    }

    private sealed class AgentEventPayload
    {
        public Guid Id { get; set; }
        public string EventType { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? MetadataJson { get; set; }
        public DateTime Timestamp { get; set; }
    }

    private sealed class MachineMetricsPayload
    {
        public Guid Id { get; set; }
        public double CpuPercent { get; set; }
        public long MemoryUsedMb { get; set; }
        public long MemoryTotalMb { get; set; }
        public double DiskUsedGb { get; set; }
        public double DiskTotalGb { get; set; }
        public DateTime SampledAt { get; set; }
    }

    private sealed class IngestResponseDto
    {
        public int Processed { get; set; }
        public int Duplicates { get; set; }
    }

    #endregion
}
