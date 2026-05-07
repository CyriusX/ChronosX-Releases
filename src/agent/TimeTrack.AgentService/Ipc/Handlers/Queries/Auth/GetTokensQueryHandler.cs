using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Queries.Auth;

/// <summary>
/// Returns the current tokens stored in the Agent's DPAPI store.
/// Used by the UI on startup to detect if the Agent has newer valid tokens
/// (prevents the UI from using a revoked refresh token after the Agent rotated it).
/// </summary>
public sealed class GetTokensQueryHandler : IpcHandlerBase, IIpcQueryHandler
{
    public string QueryName => "GetTokens";

    private readonly ITokenStore _tokenStore;
    private readonly ILogger<GetTokensQueryHandler> _logger;

    public GetTokensQueryHandler(
        ITokenStore tokenStore,
        ILogger<GetTokensQueryHandler> logger)
    {
        _tokenStore = tokenStore;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var jwt = await _tokenStore.GetJwtAsync(ct);
            var refreshToken = await _tokenStore.GetRefreshTokenAsync(ct);

            if (string.IsNullOrEmpty(jwt))
            {
                return SuccessResponse(request.RequestId, new { hasTokens = false });
            }

            var expiresIn = GetExpiresInSeconds(jwt);

            return SuccessResponse(request.RequestId, new
            {
                hasTokens = true,
                accessToken = jwt,
                refreshToken = refreshToken ?? string.Empty,
                expiresIn
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tokens");
            return UnknownErrorResponse(request.RequestId, ex);
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
