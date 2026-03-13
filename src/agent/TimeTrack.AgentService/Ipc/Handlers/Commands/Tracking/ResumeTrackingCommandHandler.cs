using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles resuming tracking
/// </summary>
public sealed class ResumeTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ResumeTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly ILogger<ResumeTrackingCommandHandler> _logger;

    public ResumeTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        ILogger<ResumeTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _trackingControl.ResumeAsync(new ResumeTrackingRequest
            {
                ResumedBy = "DesktopHost"
            }, ct);

            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming tracking");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
