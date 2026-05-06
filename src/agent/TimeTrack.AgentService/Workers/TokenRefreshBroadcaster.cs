using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Subscribes to ITokenStore token lifecycle events and forwards them to the
/// DesktopHost (and ultimately the WebView2 UI).
///
/// - TokensStored  -> tokensRefreshed event (keeps the UI's localStorage in sync
///   after the Agent rotates the refresh token against the single-use backend).
/// - TokensCleared -> sessionRevoked event (the Agent only clears tokens when
///   the backend tells it the refresh token is permanently invalid; the UI uses
///   this as a signal to log out the user, as opposed to a transient error).
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
        _tokenStore.TokensCleared += OnTokensCleared;
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

    private async void OnTokensCleared(object? sender, EventArgs e)
    {
        try
        {
            if (!_ipcServer.IsClientConnected)
                return;

            _logger.LogInformation("Broadcasting sessionRevoked event to DesktopHost");

            await _ipcServer.SendEventAsync(
                new IpcEvent
                {
                    EventType = "sessionRevoked",
                    Payload = new { reason = "refresh_token_revoked" }
                },
                CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting sessionRevoked event");
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
