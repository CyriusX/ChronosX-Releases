using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Subscribes to ITokenStore.TokensStored and broadcasts new tokens to the
/// DesktopHost (and ultimately the WebView2 UI) so that the React app's
/// localStorage stays in sync after the Agent refreshes tokens.
///
/// The backend uses single-use refresh-token rotation. When the Agent calls
/// RefreshAsync(), the old refresh token is revoked. Without this broadcaster
/// the UI would hold a stale, revoked refresh token and fail to restore the
/// session on the next app start.
/// </summary>
public sealed class TokenRefreshBroadcaster
{
    private readonly IIpcServer _ipcServer;
    private readonly ITokenStore _tokenStore;
    private readonly ILogger<TokenRefreshBroadcaster> _logger;

    public TokenRefreshBroadcaster(
        IIpcServer ipcServer,
        ITokenStore tokenStore,
        ILogger<TokenRefreshBroadcaster> logger)
    {
        _ipcServer = ipcServer;
        _tokenStore = tokenStore;
        _logger = logger;

        _tokenStore.TokensStored += OnTokensStored;
    }

    private async void OnTokensStored(object? sender, TokensStoredEventArgs e)
    {
        try
        {
            if (!_ipcServer.IsClientConnected)
                return;

            _logger.LogDebug("Broadcasting tokensRefreshed event to DesktopHost");

            // Extract expiresIn from the new JWT so the UI can set the correct expiresAt
            var expiresIn = GetExpiresInSeconds(e.Jwt);

            var payload = new
            {
                accessToken = e.Jwt,
                refreshToken = e.RefreshToken,
                expiresIn
            };

            await _ipcServer.SendEventAsync(
                new IpcEvent { EventType = "tokensRefreshed", Payload = payload },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting tokensRefreshed event");
        }
    }

    private static long GetExpiresInSeconds(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return 3600;

            var payload = parts[1];
            var padding = payload.Length % 4;
            if (padding > 0) payload += new string('=', 4 - padding);

            payload = payload.Replace('-', '+').Replace('_', '/');
            var jsonBytes = Convert.FromBase64String(payload);
            var json = System.Text.Encoding.UTF8.GetString(jsonBytes);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("exp", out var expElement))
            {
                var exp = expElement.GetInt64();
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return Math.Max(exp - now, 60);
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return 3600;
    }
}
