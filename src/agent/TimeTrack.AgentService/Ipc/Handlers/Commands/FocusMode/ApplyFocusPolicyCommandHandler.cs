using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles applying focus mode policy
/// </summary>
public sealed class ApplyFocusPolicyCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ApplyFocusPolicy";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<ApplyFocusPolicyCommandHandler> _logger;

    public ApplyFocusPolicyCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<ApplyFocusPolicyCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            FocusModePolicy? policy = null;

            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                var payload = request.Payload.Value;
                policy = JsonSerializer.Deserialize<FocusModePolicy>(payload.GetRawText());
            }

            if (policy is null)
            {
                return ErrorResponse(request.RequestId, "Invalid focus mode policy payload");
            }

            _focusModeEngine.ApplyPolicy(policy);
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                applied = true,
                state = snapshot.State.ToString(),
                mode = snapshot.Mode.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying focus policy");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
