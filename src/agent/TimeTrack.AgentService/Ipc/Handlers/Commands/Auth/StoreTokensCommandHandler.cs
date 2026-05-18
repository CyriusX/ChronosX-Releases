using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Auth;

/// <summary>
/// Handles storing authentication tokens and activating the device
/// </summary>
public sealed class StoreTokensCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StoreTokens";

    private readonly ITokenStore _tokenStore;
    private readonly ICurrentUserContext _userContext;
    private readonly IDeviceActivationService _deviceActivationService;
    private readonly ILogger<StoreTokensCommandHandler> _logger;

    public StoreTokensCommandHandler(
        ITokenStore tokenStore,
        ICurrentUserContext userContext,
        IDeviceActivationService deviceActivationService,
        ILogger<StoreTokensCommandHandler> logger)
    {
        _tokenStore = tokenStore;
        _userContext = userContext;
        _deviceActivationService = deviceActivationService;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        var (jwt, refreshToken) = ExtractTokens(request);

        if (string.IsNullOrEmpty(jwt))
        {
            return ErrorResponse(request.RequestId, "Access token is required");
        }

        // Activate device on backend (this will store new tokens with device_id)
        var activationResult = await _deviceActivationService.ActivateDeviceAsync(
            jwt,
            refreshToken ?? string.Empty,
            ct);

        if (!activationResult.IsSuccess)
        {
            _logger.LogWarning(
                "Device activation failed ({Error}), falling back to storing original tokens so local tracking can start",
                activationResult.ErrorMessage);

            // Fallback: store original JWT so UserId is available and tracking can start locally
            await _tokenStore.StoreTokensAsync(jwt, refreshToken ?? string.Empty, ct);
        }

        // Refresh user context with new tokens
        await _userContext.RefreshAsync(ct);

        if (!_userContext.IsAuthenticated)
        {
            _logger.LogWarning("Tokens stored but user context not authenticated");
            return ErrorResponse(request.RequestId, "Failed to authenticate user from token");
        }

        _logger.LogInformation(
            "Tokens stored successfully for user {UserId}, device {DeviceId}",
            _userContext.UserId,
            activationResult.DeviceId);

        return SuccessResponse(request.RequestId, new
        {
            stored = true,
            userId = _userContext.UserId,
            deviceId = activationResult.DeviceId,
            activationStatus = activationResult.IsSuccess ? activationResult.Status : "fallback"
        });
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
