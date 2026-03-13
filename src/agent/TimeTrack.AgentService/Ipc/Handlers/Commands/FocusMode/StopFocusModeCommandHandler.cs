using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles stopping focus mode
/// </summary>
public sealed class StopFocusModeCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StopFocusMode";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<StopFocusModeCommandHandler> _logger;

    public StopFocusModeCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<StopFocusModeCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            _focusModeEngine.Stop();
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                stopped = true,
                state = snapshot.State.ToString()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping focus mode");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
