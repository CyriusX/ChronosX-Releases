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

    // Coalesces concurrent RefreshAsync callers (e.g. Agent SyncWorker + UI IPC request)
    // so only one HTTP call to /auth/refresh is in-flight at a time. Without this the
    // two would race, one would get 400 "revoked" from the backend's single-use rotation,
    // and wipe tokens the user is actively using.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private sealed record TokenData(string Jwt, string RefreshToken, DateTime ExpiresAt);

    public event EventHandler<TokensStoredEventArgs>? TokensStored;
    public event EventHandler? TokensCleared;

    public DpapiTokenStore(
        ILogger<DpapiTokenStore> logger,
        HttpClient httpClient,
        Contracts.Configuration.SyncSettings settings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _backendUrl = settings.BackendUrl;

        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var timeTrackPath = Path.Combine(appDataPath, "TimeTrack");

        Directory.CreateDirectory(timeTrackPath);
        _tokenFilePath = Path.Combine(timeTrackPath, "tokens.dat");
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
        // Snapshot the refresh token the caller saw before they decided to refresh.
        // Combined with the semaphore below, this lets the second caller short-circuit
        // when a concurrent refresh has already rotated the token on disk.
        var refreshTokenAtEntry = await GetRefreshTokenAsync(cancellationToken);

        if (string.IsNullOrEmpty(refreshTokenAtEntry))
        {
            _logger.LogWarning("No refresh token available");
            return false;
        }

        await _refreshGate.WaitAsync(cancellationToken);
        try
        {
            var refreshToken = await GetRefreshTokenAsync(cancellationToken);
            if (string.IsNullOrEmpty(refreshToken))
            {
                _logger.LogWarning("No refresh token available after acquiring refresh gate");
                return false;
            }

            // If another caller rotated the token while we waited on the gate, the
            // token in the store is already fresh — don't fire a second /auth/refresh
            // that would 400 against the backend's single-use rotation.
            if (!string.Equals(refreshToken, refreshTokenAtEntry, StringComparison.Ordinal))
            {
                _logger.LogDebug("Token was already refreshed by a concurrent caller — skipping HTTP call");
                return true;
            }

            var request = new { refreshToken = refreshToken };
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"{_backendUrl}/api/v1/auth/refresh",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Invalidate cache so next attempt reads fresh tokens from disk
                // (DesktopHost may have stored new tokens via login/refresh).
                lock (_cacheLock)
                {
                    _cachedTokens = null;
                }

                var status = (int)response.StatusCode;

                // Only clear on signals that definitively mean "this refresh token
                // will never work again": 401 (user deactivated) or 400 with the
                // backend's validation_failed code ("expired or revoked"). Everything
                // else — 408, 429, transient 400 from upstream middleware, and any
                // 5xx — is retryable; keep the token on disk so the next cycle can
                // recover without forcing the user to log in again.
                if (await IsTerminalRefreshFailureAsync(response, cancellationToken))
                {
                    _logger.LogError(
                        "Refresh token permanently invalid ({StatusCode}) — clearing stored tokens so user must re-authenticate",
                        response.StatusCode);
                    await ClearAsync(cancellationToken);
                    return false;
                }

                _logger.LogWarning("Token refresh failed with status {StatusCode} (will retry)", response.StatusCode);
                return false;
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var refreshResponse = JsonSerializer.Deserialize<RefreshTokenResponse>(responseBody, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (refreshResponse is null || string.IsNullOrEmpty(refreshResponse.AccessToken))
            {
                _logger.LogError("Invalid refresh response");
                return false;
            }

            await StoreTokensAsync(
                refreshResponse.AccessToken,
                refreshResponse.RefreshToken,
                cancellationToken);

            _logger.LogInformation("Token refreshed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return false;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static async Task<bool> IsTerminalRefreshFailureAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return true;

        if (response.StatusCode != System.Net.HttpStatusCode.BadRequest)
            return false;

        // Backend shape for revoked/expired refresh token (see ExceptionHandlingMiddleware +
        // ValidationException in RefreshTokenCommandHandler): { code: "validation_failed", ... }.
        try
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (string.IsNullOrEmpty(body)) return false;

            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("code", out var codeProp))
            {
                var code = codeProp.GetString();
                if (string.Equals(code, "validation_failed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(code, "user_deactivated", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Body wasn't JSON or was already consumed — be safe and retry.
        }

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
