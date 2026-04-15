using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// HTTP client for kanban task endpoints. Handles JWT auth + single retry on 401.
/// </summary>
public sealed class BackendTasksClient : IBackendTasksClient
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<BackendTasksClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public BackendTasksClient(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ILogger<BackendTasksClient> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public Task<TasksListResult?> ListMyTasksAsync(bool includeDone, CancellationToken ct = default)
        => GetJsonAsync<TasksListResult>($"/api/v1/me/tasks?includeDone={(includeDone ? "true" : "false")}", ct);

    public Task<OpenTaskResult?> GetMyOpenTaskAsync(CancellationToken ct = default)
        => GetJsonAsync<OpenTaskResult>("/api/v1/me/tasks/open", ct);

    public async Task<bool> PauseMyOpenTaskAsync(CancellationToken ct = default)
        => (await PostAsync("/api/v1/me/tasks/open/pause", null, ct))?.IsSuccessStatusCode ?? false;

    public async Task<bool> ResumeMyOpenTaskAsync(CancellationToken ct = default)
        => (await PostAsync("/api/v1/me/tasks/open/resume", null, ct))?.IsSuccessStatusCode ?? false;

    public async Task<bool> CloseOpenTaskOnIdleRejectAsync(CancellationToken ct = default)
        => (await PostAsync("/api/v1/me/tasks/open/close-on-idle-reject", null, ct))?.IsSuccessStatusCode ?? false;

    public async Task<TaskDetail?> MoveTaskAsync(Guid taskId, string status, uint? rowVersion, CancellationToken ct = default)
    {
        var body = new { status, position = (double?)null, rowVersion };
        var response = await SendAsync(HttpMethod.Patch, $"/api/v1/tasks/{taskId}/move", body, ct);
        if (response is null || !response.IsSuccessStatusCode) return null;
        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<TaskDetail>(json, JsonOptions);
    }

    public Task<NotificationsListResult?> ListMyNotificationsAsync(bool unreadOnly, int take, CancellationToken ct = default)
        => GetJsonAsync<NotificationsListResult>($"/api/v1/me/notifications?unreadOnly={(unreadOnly ? "true" : "false")}&take={take}", ct);

    public async Task<bool> MarkNotificationReadAsync(Guid notificationId, CancellationToken ct = default)
        => (await PostAsync($"/api/v1/me/notifications/{notificationId}/read", null, ct))?.IsSuccessStatusCode ?? false;

    public Task<ProjectListResult?> ListProjectsAsync(bool activeOnly, CancellationToken ct = default)
        => GetJsonAsync<ProjectListResult>($"/api/v1/projects?activeOnly={(activeOnly ? "true" : "false")}", ct);

    // ─────────────────────────────────────────────────────────────────────
    // Private HTTP helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<T?> GetJsonAsync<T>(string path, CancellationToken ct) where T : class
    {
        var response = await SendAsync(HttpMethod.Get, path, null, ct);
        if (response is null) return null;
        if (response.StatusCode == HttpStatusCode.NoContent) return null;
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("[BackendTasks] GET {Path} failed: {Status}", path, response.StatusCode);
            return null;
        }
        var json = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private Task<HttpResponseMessage?> PostAsync(string path, object? body, CancellationToken ct)
        => SendAsync(HttpMethod.Post, path, body, ct);

    private async Task<HttpResponseMessage?> SendAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        try
        {
            if (_tokenStore.IsJwtExpiringSoon(2))
                await _tokenStore.RefreshAsync(ct);

            var jwt = await _tokenStore.GetJwtAsync(ct);
            if (string.IsNullOrEmpty(jwt))
            {
                _logger.LogWarning("[BackendTasks] No JWT available");
                return null;
            }

            var response = await SendOnceAsync(method, path, body, jwt, ct);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("[BackendTasks] 401 from {Path}, refreshing token and retrying", path);
                response.Dispose();
                var refreshed = await _tokenStore.RefreshAsync(ct);
                if (!refreshed) return null;
                jwt = await _tokenStore.GetJwtAsync(ct);
                if (string.IsNullOrEmpty(jwt)) return null;
                response = await SendOnceAsync(method, path, body, jwt, ct);
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BackendTasks] {Method} {Path} threw", method, path);
            return null;
        }
    }

    private async Task<HttpResponseMessage> SendOnceAsync(HttpMethod method, string path, object? body, string jwt, CancellationToken ct)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }
        return await _httpClient.SendAsync(request, ct);
    }
}
