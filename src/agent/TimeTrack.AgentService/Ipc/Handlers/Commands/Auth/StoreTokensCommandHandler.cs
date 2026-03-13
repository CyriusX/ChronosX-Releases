using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Auth;

/// <summary>
/// Handles storing authentication tokens
/// </summary>
public sealed class StoreTokensCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StoreTokens";

    private readonly ITokenStore _tokenStore;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<StoreTokensCommandHandler> _logger;

    public StoreTokensCommandHandler(
        ITokenStore tokenStore,
        ICurrentUserContext userContext,
        ILogger<StoreTokensCommandHandler> logger)
    {
        _tokenStore = tokenStore;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var (jwt, refreshToken) = ExtractTokens(request);

            if (string.IsNullOrEmpty(jwt))
            {
                return ErrorResponse(request.RequestId, "Access token is required");
            }

            await _tokenStore.StoreTokensAsync(jwt, refreshToken ?? string.Empty, ct);
            await _userContext.RefreshAsync(ct);

            if (!_userContext.IsAuthenticated)
            {
                _logger.LogWarning("Tokens stored but user context not authenticated");
                return ErrorResponse(request.RequestId, "Failed to authenticate user from token");
            }

            _logger.LogInformation("Tokens stored successfully for user {UserId}", _userContext.UserId);
            return SuccessResponse(request.RequestId, new { stored = true, userId = _userContext.UserId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing tokens");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    private static (string? Jwt, string? RefreshToken) ExtractTokens(IpcRequest request)
    {
        string? jwt = null;
        string? refreshToken = null;

        if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
        {
            var payload = request.Payload.Value;
            if (payload.TryGetProperty("accessToken", out var jwtProp))
                jwt = jwtProp.GetString();
            if (payload.TryGetProperty("refreshToken", out var refreshProp))
                refreshToken = refreshProp.GetString();
        }

        return (jwt, refreshToken);
    }
}
