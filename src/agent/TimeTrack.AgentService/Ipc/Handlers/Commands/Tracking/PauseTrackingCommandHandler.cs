using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles pausing tracking
/// </summary>
public sealed class PauseTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "PauseTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly ILogger<PauseTrackingCommandHandler> _logger;

    public PauseTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        ILogger<PauseTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            string? reason = null;
            if (request.Payload.HasValue && request.Payload.Value.ValueKind == JsonValueKind.Object)
            {
                reason = request.Payload.Value.GetProperty("reason").GetString();
            }

            var result = await _trackingControl.PauseAsync(new PauseTrackingRequest
            {
                Reason = reason ?? "User requested",
                PausedBy = "DesktopHost"
            }, ct);

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing tracking");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
