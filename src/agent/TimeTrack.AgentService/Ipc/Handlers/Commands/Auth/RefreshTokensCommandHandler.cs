using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Auth;

/// <summary>
/// Performs a refresh against /auth/refresh on behalf of the UI so that the
/// Agent is the single process holding the rotating refresh token. The UI's
/// apiClient calls this on 401 instead of hitting /auth/refresh directly —
/// that way the Agent's DPAPI store never falls out of sync with the backend.
/// </summary>
public sealed class RefreshTokensCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "RefreshTokens";

    private readonly ITokenStore _tokenStore;
    private readonly ILogger<RefreshTokensCommandHandler> _logger;

    public RefreshTokensCommandHandler(
        ITokenStore tokenStore,
        ILogger<RefreshTokensCommandHandler> logger)
    {
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        var refreshed = await _tokenStore.RefreshAsync(ct);
        if (!refreshed)
        {
            return ErrorResponse(request.RequestId, "refresh_failed");
        }

        var jwt = await _tokenStore.GetJwtAsync(ct);
        var refreshToken = await _tokenStore.GetRefreshTokenAsync(ct);

        if (string.IsNullOrEmpty(jwt))
        {
            return ErrorResponse(request.RequestId, "no_tokens_after_refresh");
        }

        var expiresIn = GetExpiresInSeconds(jwt);

        return SuccessResponse(request.RequestId, new
        {
            accessToken = jwt,
            refreshToken = refreshToken ?? string.Empty,
            expiresIn
        });
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
