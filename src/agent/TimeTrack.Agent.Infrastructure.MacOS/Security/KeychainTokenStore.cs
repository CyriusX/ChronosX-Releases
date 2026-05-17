using System.Diagnostics;
using System.Net;
using System.Runtime.Versioning;
using System.Security;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Infrastructure.MacOS.Security;

/// <summary>
/// Implementation of ITokenStore using macOS Keychain for secure storage.
/// Uses the /usr/bin/security CLI tool for reliable Keychain access.
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class KeychainTokenStore : ITokenStore
{
    private const string DefaultServiceName = "com.cyriusx.timetrack";
    private const string DefaultTokenAccount = "jwt_tokens";

    private readonly ILogger<KeychainTokenStore> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _backendUrl;
    private readonly string _serviceName;
    private readonly string _tokenAccount;

    private TokenData? _cachedTokens;
    private readonly object _cacheLock = new();

    // Coalesces concurrent RefreshAsync callers so only one HTTP call to /auth/refresh is
    // in-flight at a time. Without this, two consumers can race, one will get a 400 from
    // the backend's single-use refresh-token rotation, and we'd clear tokens unnecessarily.
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private sealed record TokenData(string Jwt, string RefreshToken, DateTime ExpiresAt);

    public event EventHandler<TokensStoredEventArgs>? TokensStored;
    public event EventHandler? TokensCleared;

    public KeychainTokenStore(
        ILogger<KeychainTokenStore> logger,
        HttpClient httpClient,
        Contracts.Configuration.SyncSettings settings,
        string? serviceNameOverride = null,
        string? tokenAccountOverride = null)
    {
        _logger = logger;
        _httpClient = httpClient;
        _backendUrl = settings.BackendUrl;
        _serviceName = string.IsNullOrWhiteSpace(serviceNameOverride) ? DefaultServiceName : serviceNameOverride;
        _tokenAccount = string.IsNullOrWhiteSpace(tokenAccountOverride) ? DefaultTokenAccount : tokenAccountOverride;
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

        var json = JsonSerializer.Serialize(tokens);
        var success = KeychainStore(_serviceName, _tokenAccount, json);

        if (!success)
        {
            _logger.LogError("Failed to store tokens in Keychain");
            throw new SecurityException("Failed to store tokens securely");
        }

        lock (_cacheLock)
        {
            _cachedTokens = tokens;
        }

        _logger.LogInformation("Tokens stored securely in macOS Keychain");

        TokensStored?.Invoke(this, new TokensStoredEventArgs
        {
            Jwt = jwt,
            RefreshToken = refreshToken
        });
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        KeychainDelete(_serviceName, _tokenAccount);

        lock (_cacheLock)
        {
            _cachedTokens = null;
        }

        _logger.LogInformation("Tokens cleared from Keychain");
        TokensCleared?.Invoke(this, EventArgs.Empty);

        await Task.CompletedTask;
    }

    public bool IsJwtExpiringSoon(int withinMinutes = 5)
    {
        lock (_cacheLock)
        {
            if (_cachedTokens is null)
                return true;
            return _cachedTokens.ExpiresAt <= DateTime.UtcNow.AddMinutes(withinMinutes);
        }
    }

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        // Snapshot the refresh token the caller saw before they decided to refresh.
        // Combined with the semaphore below, this lets the second caller short-circuit
        // when a concurrent refresh has already rotated the token in the store.
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
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(
                $"{_backendUrl}/api/v1/auth/refresh",
                content,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Invalidate cache so next attempt reads fresh tokens from the store.
                lock (_cacheLock)
                {
                    _cachedTokens = null;
                }

                // Terminal failures:
                // - 401: user deactivated / refresh token invalid
                // - 400: refresh token invalid/expired/revoked (single-use rotation)
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
                {
                    _logger.LogError(
                        "Token refresh failed with terminal status {StatusCode} — clearing stored tokens so user must re-authenticate",
                        response.StatusCode);
                    await ClearAsync(cancellationToken);
                    return false;
                }

                _logger.LogWarning("Token refresh failed with status {StatusCode}", response.StatusCode);
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

            var refreshTokenToStore = string.IsNullOrWhiteSpace(refreshResponse.RefreshToken)
                ? refreshToken
                : refreshResponse.RefreshToken;

            await StoreTokensAsync(
                refreshResponse.AccessToken,
                refreshTokenToStore,
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

    private async Task<TokenData?> LoadTokensAsync(CancellationToken cancellationToken)
    {
        lock (_cacheLock)
        {
            if (_cachedTokens is not null)
                return _cachedTokens;
        }

        var json = KeychainFind(_serviceName, _tokenAccount);
        if (json == null)
            return null;

        try
        {
            var tokens = JsonSerializer.Deserialize<TokenData>(json);
            lock (_cacheLock)
            {
                _cachedTokens = tokens;
            }
            return tokens;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize tokens from Keychain");
            return null;
        }
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
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            var payloadObj = JsonSerializer.Deserialize<JsonElement>(json);

            if (payloadObj.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(exp).UtcDateTime;
            }
        }
        catch
        {
        }

        return DateTime.UtcNow.AddHours(1);
    }

    // =========================================================================
    // Keychain access via /usr/bin/security CLI
    // =========================================================================

    /// <summary>
    /// Stores (or updates) a generic password in the macOS Keychain.
    /// Uses -U flag to update if the entry already exists.
    /// </summary>
    private bool KeychainStore(string service, string account, string data)
    {
        try
        {
            // -U = update if exists, -s = service, -a = account, -w = password data
            var (exitCode, _, stderr) = RunSecurity(
                "add-generic-password",
                "-U",
                "-s", service,
                "-a", account,
                "-w", data);

            if (exitCode != 0)
            {
                _logger.LogError("security add-generic-password failed (exit {ExitCode}): {Stderr}",
                    exitCode, stderr);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing to Keychain via security CLI");
            return false;
        }
    }

    /// <summary>
    /// Reads a generic password from the macOS Keychain.
    /// Returns the password string, or null if not found.
    /// </summary>
    private string? KeychainFind(string service, string account)
    {
        try
        {
            // -s = service, -a = account, -w = output only the password
            var (exitCode, stdout, _) = RunSecurity(
                "find-generic-password",
                "-s", service,
                "-a", account,
                "-w");

            if (exitCode != 0)
                return null;

            var result = stdout.Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from Keychain via security CLI");
            return null;
        }
    }

    /// <summary>
    /// Deletes a generic password from the macOS Keychain.
    /// </summary>
    private void KeychainDelete(string service, string account)
    {
        try
        {
            RunSecurity(
                "delete-generic-password",
                "-s", service,
                "-a", account);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting from Keychain via security CLI");
        }
    }

    /// <summary>
    /// Runs /usr/bin/security with the given arguments and returns (exitCode, stdout, stderr).
    /// </summary>
    private static (int ExitCode, string Stdout, string Stderr) RunSecurity(params string[] args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "/usr/bin/security",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var arg in args)
            psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(10_000); // 10 second timeout

        return (process.ExitCode, stdout, stderr);
    }

    private sealed class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }
}
