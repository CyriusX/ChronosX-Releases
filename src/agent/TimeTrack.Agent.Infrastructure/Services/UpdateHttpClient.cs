using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Contracts.Updates;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// HTTP client for update operations
/// </summary>
public sealed class UpdateHttpClient : IUpdateHttpClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly UpdateSettings _settings;
    private readonly ILogger<UpdateHttpClient> _logger;
    private readonly ITokenStore? _tokenStore;
    private readonly JsonSerializerOptions _jsonOptions;

    public UpdateHttpClient(
        HttpClient httpClient,
        UpdateSettings settings,
        ILogger<UpdateHttpClient> logger,
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
        // Extract base URL from UpdateUrl (e.g., "https://chronosx-dev-timetrack-api.gpoda0.easypanel.host" from "https://chronosx-dev-timetrack-api.gpoda0.easypanel.host/api/v1/updates")
        var updateUrl = _settings.UpdateUrl;
        var uri = new Uri(updateUrl);
        var baseUrl = $"{uri.Scheme}://{uri.Host}";

        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromMinutes(_settings.DownloadTimeoutMinutes);
    }

    /// <summary>
    /// Ensures valid JWT token before making requests
    /// </summary>
    private async Task<bool> EnsureValidTokenAsync(CancellationToken cancellationToken)
    {
        if (_tokenStore is null)
            return true;

        if (_tokenStore.IsJwtExpiringSoon(5))
        {
            _logger.LogInformation("JWT expiring soon, attempting refresh");
            var refreshed = await _tokenStore.RefreshAsync(cancellationToken);

            if (!refreshed)
            {
                _logger.LogWarning("Failed to refresh token for update check - proceeding without auth");
                return true; // Proceed without auth - update endpoints are anonymous
            }
        }

        var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
        if (!string.IsNullOrEmpty(jwt))
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);
            return true;
        }

        return false;
    }

    public async Task<UpdateCheckResponse?> CheckForUpdatesAsync(
        string currentVersion,
        string channel,
        CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (true)
        {
            attempt++;

            try
            {
                if (!await EnsureValidTokenAsync(cancellationToken))
                {
                    _logger.LogWarning("Cannot check for updates: authentication unavailable");
                    return null;
                }

                var requestPath = $"/api/v1/updates/check?currentVersion={Uri.EscapeDataString(currentVersion)}&channel={Uri.EscapeDataString(channel)}";

                _logger.LogInformation("Checking for updates: {Path} (attempt {Attempt})", requestPath, attempt);

                var response = await _httpClient.GetAsync(requestPath, cancellationToken);

                // Handle 401 - try refresh and retry once
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized && _tokenStore is not null)
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

                        response = await _httpClient.GetAsync(requestPath, cancellationToken);
                    }
                }

                // 204 = no update available
                if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    _logger.LogInformation("No update available");
                    return new UpdateCheckResponse
                    {
                        HasUpdate = false,
                        CurrentVersion = currentVersion
                    };
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Update check failed: StatusCode={StatusCode}, Error={Error}",
                        response.StatusCode, errorContent);
                    return null;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var result = JsonSerializer.Deserialize<UpdateCheckResponse>(content, _jsonOptions);

                if (result != null)
                {
                    _logger.LogInformation("Update check result: HasUpdate={HasUpdate}, LatestVersion={LatestVersion}",
                        result.HasUpdate, result.LatestVersion);
                }

                return result;
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= _settings.MaxRetryAttempts)
                {
                    _logger.LogError(ex, "Update check failed after {Attempts} attempts", attempt);
                    return null;
                }

                _logger.LogWarning(ex, "Update check attempt {Attempt} failed, retrying in {Delay}s",
                    attempt, _settings.RetryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(_settings.RetryDelaySeconds), cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt >= _settings.MaxRetryAttempts)
                {
                    _logger.LogWarning("Timeout checking for updates after {Attempts} attempts", attempt);
                    return null;
                }

                _logger.LogWarning("Timeout on attempt {Attempt}, retrying in {Delay}s",
                    attempt, _settings.RetryDelaySeconds);
                await Task.Delay(TimeSpan.FromSeconds(_settings.RetryDelaySeconds), cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking for updates");
                return null;
            }
        }
    }

    public async Task<string> DownloadUpdateAsync(
        string downloadUrl,
        string destinationPath,
        IProgress<UpdateDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Downloading update from: {Url}", downloadUrl);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);

        // Add authorization if we have a token store
        if (_tokenStore != null)
        {
            var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
            if (!string.IsNullOrEmpty(jwt))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
            }
        }

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;
        var totalBytesRead = 0L;
        var startTime = DateTime.UtcNow;
        var lastReportTime = startTime;

        await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var fileStream = new FileStream(
            destinationPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 8192,
            useAsync: true);

        var buffer = new byte[8192];
        var bytesRead = 0;

        while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
            totalBytesRead += bytesRead;

            // Report progress every 100ms
            var now = DateTime.UtcNow;
            if (now - lastReportTime > TimeSpan.FromMilliseconds(100))
            {
                var elapsed = now - startTime;
                var bytesPerSecond = elapsed.TotalSeconds > 0
                    ? (long)(totalBytesRead / elapsed.TotalSeconds)
                    : 0;

                TimeSpan? estimatedTimeRemaining = null;
                if (bytesPerSecond > 0 && totalBytes > 0)
                {
                    var remainingBytes = totalBytes - totalBytesRead;
                    estimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / bytesPerSecond);
                }

                progress?.Report(new UpdateDownloadProgress
                {
                    BytesDownloaded = totalBytesRead,
                    BytesTotal = totalBytes,
                    BytesPerSecond = bytesPerSecond,
                    EstimatedTimeRemaining = estimatedTimeRemaining
                });

                lastReportTime = now;
            }
        }

        // Final progress report
        progress?.Report(new UpdateDownloadProgress
        {
            BytesDownloaded = totalBytesRead,
            BytesTotal = totalBytes,
            BytesPerSecond = 0,
            EstimatedTimeRemaining = TimeSpan.Zero
        });

        _logger.LogInformation("Download completed: {Bytes} bytes to {Path}", totalBytesRead, destinationPath);

        return destinationPath;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
