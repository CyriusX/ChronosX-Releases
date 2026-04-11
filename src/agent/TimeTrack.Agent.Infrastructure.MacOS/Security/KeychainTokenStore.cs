using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Infrastructure.MacOS.Interop;

namespace TimeTrack.Agent.Infrastructure.MacOS.Security;

/// <summary>
/// Implementation of ITokenStore using macOS Keychain for secure storage
/// </summary>
[SupportedOSPlatform("macos")]
public sealed class KeychainTokenStore : ITokenStore
{
    private const string ServiceName = "com.cyriusx.timetrack";
    private const string TokenAccount = "jwt_tokens";

    private readonly ILogger<KeychainTokenStore> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _backendUrl;

    private TokenData? _cachedTokens;
    private readonly object _cacheLock = new();

    private sealed record TokenData(string Jwt, string RefreshToken, DateTime ExpiresAt);

    public event EventHandler<TokensStoredEventArgs>? TokensStored;
    public event EventHandler? TokensCleared;

    public KeychainTokenStore(
        ILogger<KeychainTokenStore> logger,
        HttpClient httpClient,
        Contracts.Configuration.SyncSettings settings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _backendUrl = settings.BackendUrl;
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
        var status = SecItemAddOrUpdate(ServiceName, TokenAccount, json);

        if (status != 0)
        {
            _logger.LogError("Failed to store tokens in Keychain: {Status}", status);
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
        var status = SecItemDelete(ServiceName, TokenAccount);

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
        var refreshToken = await GetRefreshTokenAsync(cancellationToken);

        if (string.IsNullOrEmpty(refreshToken))
        {
            _logger.LogWarning("No refresh token available");
            return false;
        }

        try
        {
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
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("User deactivated or refresh token invalid");
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

            await StoreTokensAsync(
                refreshResponse.AccessToken,
                refreshResponse.RefreshToken ?? refreshToken,
                cancellationToken);

            _logger.LogInformation("Token refreshed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return false;
        }
    }

    private async Task<TokenData?> LoadTokensAsync(CancellationToken cancellationToken)
    {
        lock (_cacheLock)
        {
            if (_cachedTokens is not null)
                return _cachedTokens;
        }

        var json = SecItemCopyContent(ServiceName, TokenAccount);
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

    private static int SecItemAddOrUpdate(string service, string account, string data)
    {
        try
        {
            var existingData = SecItemCopyContent(service, account);
            if (existingData != null)
            {
                var attributes = CreateQueryDictionary(service, account);
                var updateAttributes = CreateUpdateDictionary(data);
                return SecItemUpdate(attributes, updateAttributes);
            }

            var addAttributes = CreateAddAttributes(service, account, data);
            return SecItemAdd(addAttributes, IntPtr.Zero);
        }
        catch
        {
            return -1;
        }
    }

    private static IntPtr CreateQueryDictionary(string service, string account)
    {
        var dict = CFDictionaryCreateMutable(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
        if (dict == IntPtr.Zero)
            return IntPtr.Zero;

        var serviceCf = CFStringCreate(service);
        var accountCf = CFStringCreate(account);
        var classKey = CFStringCreate("class");
        var genericPasswordClass = CFStringCreate("genp");

        CFDictionaryAddValue(dict, classKey, genericPasswordClass);
        CFDictionaryAddValue(dict, serviceCf, accountCf);

        CFRelease(serviceCf);
        CFRelease(accountCf);
        CFRelease(classKey);
        CFRelease(genericPasswordClass);

        return dict;
    }

    private static IntPtr CreateUpdateDictionary(string data)
    {
        var dict = CFDictionaryCreateMutable(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
        if (dict == IntPtr.Zero)
            return IntPtr.Zero;

        var valueKey = CFStringCreate("v_Data");
        var dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
        var dataCf = CFDataCreate(dataBytes);

        CFDictionaryAddValue(dict, valueKey, dataCf);

        CFRelease(valueKey);
        CFRelease(dataCf);

        return dict;
    }

    private static IntPtr CreateAddAttributes(string service, string account, string data)
    {
        var dict = CFDictionaryCreateMutable(IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
        if (dict == IntPtr.Zero)
            return IntPtr.Zero;

        var serviceKey = CFStringCreate("srvr");
        var accountKey = CFStringCreate("acct");
        var classKey = CFStringCreate("class");
        var valueKey = CFStringCreate("v_Data");
        var genericPasswordClass = CFStringCreate("genp");

        var serviceCf = CFStringCreate(service);
        var accountCf = CFStringCreate(account);
        var dataBytes = System.Text.Encoding.UTF8.GetBytes(data);
        var dataCf = CFDataCreate(dataBytes);

        CFDictionaryAddValue(dict, classKey, genericPasswordClass);
        CFDictionaryAddValue(dict, serviceKey, serviceCf);
        CFDictionaryAddValue(dict, accountKey, accountCf);
        CFDictionaryAddValue(dict, valueKey, dataCf);

        CFRelease(serviceKey);
        CFRelease(accountKey);
        CFRelease(classKey);
        CFRelease(valueKey);
        CFRelease(genericPasswordClass);
        CFRelease(serviceCf);
        CFRelease(accountCf);
        CFRelease(dataCf);

        return dict;
    }

    private static string? SecItemCopyContent(string service, string account)
    {
        try
        {
            var query = CreateQueryDictionary(service, account);
            var result = IntPtr.Zero;
            var status = SecItemCopyMatching(query, out result);

            if (status != 0 || result == IntPtr.Zero)
                return null;

            var dataPtr = CFDictionaryGetValue(result, CFStringCreate("v_Data"));
            if (dataPtr == IntPtr.Zero)
            {
                CFRelease(result);
                return null;
            }

            var dataBytes = CFDataGetBytePtr(dataPtr);
            var length = (int)CFDataGetLength(dataPtr);
            var data = new byte[length];
            Marshal.Copy(dataBytes, data, 0, length);

            CFRelease(result);

            return System.Text.Encoding.UTF8.GetString(data);
        }
        catch
        {
            return null;
        }
    }

    private static int SecItemDelete(string service, string account)
    {
        try
        {
            var query = CreateQueryDictionary(service, account);
            return SecItemDelete(query);
        }
        catch
        {
            return -1;
        }
    }

    private sealed class RefreshTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
    }

    #region Native Interop

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecItemAdd(IntPtr attributes, IntPtr result);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecItemUpdate(IntPtr query, IntPtr attributesToUpdate);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecItemDelete(IntPtr query);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecItemCopyMatching(IntPtr query, out IntPtr result);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryCreateMutable(IntPtr allocator, int capacity, IntPtr keyCallbacks, IntPtr valueCallbacks);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFDictionaryAddValue(IntPtr theDict, IntPtr key, IntPtr value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDictionaryGetValue(IntPtr theDict, IntPtr key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataCreate(byte[] bytes);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern IntPtr CFDataGetBytePtr(IntPtr theData);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern long CFDataGetLength(IntPtr theData);

    private static IntPtr CFStringCreate(string str) => CoreFoundationNative.CFStringCreate(str);

    #endregion
}
