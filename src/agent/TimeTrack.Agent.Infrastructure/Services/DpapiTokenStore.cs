using System.Net;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação de ITokenStore usando DPAPI para armazenamento seguro.
/// Esta implementação é específica para Windows.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiTokenStore : ITokenStore
{
    private readonly ILogger<DpapiTokenStore> _logger;
    private readonly string _tokenFilePath;
    private readonly HttpClient _httpClient;
    private readonly string _backendUrl;

    private TokenData? _cachedTokens;
    private readonly object _cacheLock = new();

    // Track consecutive refresh failures to avoid clearing tokens on transient issues
    private int _consecutiveRefreshFailures;
    private DateTime _lastRefreshAttempt = DateTime.MinValue;
    private const int MaxConsecutiveFailuresBeforeClear = 3;
    private static readonly TimeSpan[] RefreshRetryDelays = new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5)
    };

    private sealed record TokenData(string Jwt, string RefreshToken, DateTime ExpiresAt);

    public event EventHandler<TokensStoredEventArgs>? TokensStored;
    public event EventHandler? TokensCleared;

    public DpapiTokenStore(
        ILogger<DpapiTokenStore> logger,
        HttpClient httpClient,
        Contracts.Configuration.SyncSettings settings,
        string? tokenFilePathOverride = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _backendUrl = settings.BackendUrl;

        if (!string.IsNullOrWhiteSpace(tokenFilePathOverride))
        {
            var dir = Path.GetDirectoryName(tokenFilePathOverride);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _tokenFilePath = tokenFilePathOverride;
        }
        else
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var timeTrackPath = Path.Combine(appDataPath, "TimeTrack");

            Directory.CreateDirectory(timeTrackPath);
            _tokenFilePath = Path.Combine(timeTrackPath, "tokens.dat");
        }
    }

    public async Task<string?> GetJwtAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await LoadTokensAsync(cancellationToken);
        return tokens?.Jwt;
    }

    public async Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken = default)
    {
        var tokens = await LoadTokensAsync(cancellationToken);
        return tokens?.RefreshToken;
    }

    public async Task StoreTokensAsync(string jwt, string refreshToken, CancellationToken cancellationToken = default)
    {
        var expiresAt = ExtractExpirationFromJwt(jwt);
        var tokens = new TokenData(jwt, refreshToken, expiresAt);

        var existing = await LoadTokensAsync(cancellationToken);
        if (existing is not null && !string.Equals(existing.RefreshToken, refreshToken, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "StoreTokensAsync overwriting refresh token (prev={PrevPrefix}, new={NewPrefix})",
                Prefix(existing.RefreshToken),
                Prefix(refreshToken));
        }

        await PersistTokensAsync(tokens, cancellationToken);

        lock (_cacheLock)
        {
            _cachedTokens = tokens;
            _consecutiveRefreshFailures = 0; // Reset failure counter on successful token storage
        }

        _logger.LogInformation("Tokens stored securely via DPAPI");

        // Disparar evento
        TokensStored?.Invoke(this, new TokensStoredEventArgs
        {
            Jwt = jwt,
            RefreshToken = refreshToken
        });
    }

    public bool IsJwtExpiringSoon(int withinMinutes = 5)
    {
        TokenData? tokens;
        lock (_cacheLock)
        {
            tokens = _cachedTokens;
        }

        if (tokens is null)
            return true;

        return tokens.ExpiresAt <= DateTime.UtcNow.AddMinutes(withinMinutes);
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        var refreshToken = await GetRefreshTokenAsync(cancellationToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token available");
            return false;
        }

        // Rate limit refresh attempts to avoid hammering the backend during issues
        var timeSinceLastAttempt = DateTime.UtcNow - _lastRefreshAttempt;
        if (timeSinceLastAttempt < TimeSpan.FromSeconds(30))
        {
            _logger.LogDebug(
                "Token refresh skipped: last attempt was {Seconds}s ago (min 30s required)",
                timeSinceLastAttempt.TotalSeconds);
            return false;
        }

        _lastRefreshAttempt = DateTime.UtcNow;

        // Try refresh with exponential backoff retry for transient errors
        for (int attempt = 0; attempt < RefreshRetryDelays.Length; attempt++)
        {
            try
            {
                var request = new { refreshToken = refreshToken };
                var content = new StringContent(
                    JsonSerializer.Serialize(request),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.PostAsync(
                    $"{_backendUrl}/api/v1/auth/refresh",
                    content,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    var refreshResponse = JsonSerializer.Deserialize<RefreshTokenResponse>(responseBody, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });

                    if (refreshResponse is not null && !string.IsNullOrEmpty(refreshResponse.AccessToken))
                    {
                        await StoreTokensAsync(
                            refreshResponse.AccessToken,
                            refreshResponse.RefreshToken,
                            cancellationToken);

                        // Reset failure counter on success
                        lock (_cacheLock)
                        {
                            _consecutiveRefreshFailures = 0;
                        }

                        _logger.LogInformation("Token refreshed successfully");
                        return true;
                    }
                }

                // Invalidate cache so next attempt reads fresh tokens from disk
                // (DesktopHost may have stored new tokens via login/refresh)
                lock (_cacheLock)
                {
                    _cachedTokens = null;
                }

                // Handle failure responses
                var isTerminalFailure = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest;
                var isTransientFailure = response.StatusCode is HttpStatusCode.ServiceUnavailable ||
                                         response.StatusCode is HttpStatusCode.BadGateway ||
                                         response.StatusCode is HttpStatusCode.GatewayTimeout ||
                                         response.StatusCode is HttpStatusCode.RequestTimeout ||
                                         (int)response.StatusCode >= 500;

                if (isTerminalFailure)
                {
                    _consecutiveRefreshFailures++;

                    // Only clear tokens after multiple consecutive terminal failures
                    // This prevents logout due to transient network issues that return 400/401
                    if (_consecutiveRefreshFailures >= MaxConsecutiveFailuresBeforeClear)
                    {
                        _logger.LogError(
                            "Token refresh failed with terminal status {StatusCode} after {Failures} consecutive attempts — clearing stored tokens",
                            response.StatusCode, _consecutiveRefreshFailures);
                        await ClearAsync(cancellationToken);
                        return false;
                    }

                    _logger.LogWarning(
                        "Token refresh failed with terminal status {StatusCode} (attempt {Attempt}/{MaxAttempts}). Will retry after delay. Tokens NOT cleared yet in case this is transient.",
                        response.StatusCode, attempt + 1, RefreshRetryDelays.Length);
                }
                else if (isTransientFailure)
                {
                    _logger.LogWarning(
                        "Token refresh failed with transient status {StatusCode} (attempt {Attempt}/{MaxAttempts}). Retrying after {Delay}s...",
                        response.StatusCode, attempt + 1, RefreshRetryDelays.Length, RefreshRetryDelays[attempt].TotalSeconds);
                }
                else
                {
                    _logger.LogWarning(
                        "Token refresh failed with unexpected status {StatusCode}",
                        response.StatusCode);
                    return false;
                }

                // Wait before retry (except on last attempt)
                if (attempt < RefreshRetryDelays.Length - 1)
                {
                    await Task.Delay(RefreshRetryDelays[attempt], cancellationToken);
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(
                    ex, "Token refresh failed due to network error (attempt {Attempt}/{MaxAttempts}). Retrying after {Delay}s...",
                    attempt + 1, RefreshRetryDelays.Length, RefreshRetryDelays[attempt].TotalSeconds);

                if (attempt < RefreshRetryDelays.Length - 1)
                {
                    await Task.Delay(RefreshRetryDelays[attempt], cancellationToken);
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Token refresh timed out (attempt {Attempt}/{MaxAttempts}). Retrying after {Delay}s...",
                    attempt + 1, RefreshRetryDelays.Length, RefreshRetryDelays[attempt].TotalSeconds);

                if (attempt < RefreshRetryDelays.Length - 1)
                {
                    await Task.Delay(RefreshRetryDelays[attempt], cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token refresh");
                return false;
            }
        }

        // All retries exhausted
        _logger.LogError(
            "Token refresh failed after {Attempts} attempts. Tokens remain stored - will retry on next cycle.",
            RefreshRetryDelays.Length);
        return false;
    }

    private async Task<TokenData?> LoadTokensAsync(CancellationToken cancellationToken)
    {
        lock (_cacheLock)
        {
            if (_cachedTokens is not null)
                return _cachedTokens;
        }

        if (!File.Exists(_tokenFilePath))
            return null;

        try
        {
            var encryptedData = await File.ReadAllBytesAsync(_tokenFilePath, cancellationToken);
            var decryptedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
            var json = Encoding.UTF8.GetString(decryptedData);

            return JsonSerializer.Deserialize<TokenData>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tokens from DPAPI storage");
            return null;
        }
    }

    private async Task PersistTokensAsync(TokenData tokens, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(tokens);
        var data = Encoding.UTF8.GetBytes(json);
        var encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);

        await File.WriteAllBytesAsync(_tokenFilePath, encryptedData, cancellationToken);
    }

    /// <summary>
    /// Limpa todos os tokens armazenados
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(_tokenFilePath))
        {
            File.Delete(_tokenFilePath);
        }

        lock (_cacheLock)
        {
            _cachedTokens = null;
            _consecutiveRefreshFailures = 0; // Reset failure counter when tokens are cleared
        }

        _logger.LogInformation("Tokens cleared");

        // Disparar evento
        TokensCleared?.Invoke(this, EventArgs.Empty);

        await Task.CompletedTask;
    }

    private static string Prefix(string? token)
    {
        if (string.IsNullOrEmpty(token)) return "<empty>";
        return token.Length <= 8 ? "<short>" : token.Substring(0, 8);
    }

    private static DateTime ExtractExpirationFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3)
                return DateTime.UtcNow.AddHours(1);

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0)
                payload += new string('=', 4 - padding);

            var jsonBytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(jsonBytes);
            var payloadObj = JsonSerializer.Deserialize<JsonElement>(json);

            if (payloadObj.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return DateTime.UtcNow.AddHours(1);
    }

    private sealed class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
