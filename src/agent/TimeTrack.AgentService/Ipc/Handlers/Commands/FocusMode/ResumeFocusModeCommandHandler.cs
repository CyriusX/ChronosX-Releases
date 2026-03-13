using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles resuming focus mode
/// </summary>
public sealed class ResumeFocusModeCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "ResumeFocusMode";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<ResumeFocusModeCommandHandler> _logger;

    public ResumeFocusModeCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<ResumeFocusModeCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var success = _focusModeEngine.Resume();
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                resumed = success,
                state = snapshot.State.ToString(),
                remainingMs = snapshot.RemainingMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming focus mode");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
