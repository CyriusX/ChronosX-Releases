using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.UseCases.TrackingControl;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles starting tracking
/// </summary>
public sealed class StartTrackingCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StartTracking";

    private readonly TrackingControlUseCase _trackingControl;
    private readonly ICurrentUserContext _userContext;
    private readonly ILogger<StartTrackingCommandHandler> _logger;

    public StartTrackingCommandHandler(
        TrackingControlUseCase trackingControl,
        ICurrentUserContext userContext,
        ILogger<StartTrackingCommandHandler> logger)
    {
        _trackingControl = trackingControl;
        _userContext = userContext;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("StartTrackingCommandHandler: HandleAsync started");

        try
        {
            _logger.LogInformation("StartTrackingCommandHandler: IsAuthenticated = {IsAuthenticated}, UserId = {UserId}",
                _userContext.IsAuthenticated, _userContext.UserId);

            if (!_userContext.IsAuthenticated)
            {
                _logger.LogWarning("StartTrackingCommandHandler: User not authenticated, returning error");
                return ErrorResponse(request.RequestId, "User not authenticated");
            }

            _logger.LogInformation("StartTrackingCommandHandler: Calling TrackingControlUseCase.StartAsync");
            var result = await _trackingControl.StartAsync(new StartTrackingRequest
            {
                StartedBy = "DesktopHost"
            }, ct);

            _logger.LogInformation("StartTrackingCommandHandler: StartAsync completed, Status = {Status}", result.Status);
            return SuccessResponse(request.RequestId, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting tracking");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
