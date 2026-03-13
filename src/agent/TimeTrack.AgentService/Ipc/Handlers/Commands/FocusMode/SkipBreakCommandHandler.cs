using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles skipping break
/// </summary>
public sealed class SkipBreakCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "SkipBreak";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<SkipBreakCommandHandler> _logger;

    public SkipBreakCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<SkipBreakCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
            var success = _focusModeEngine.SkipBreak();
            var snapshot = _focusModeEngine.GetSnapshot();

            return SuccessResponse(request.RequestId, new
            {
                skipped = success,
                state = snapshot.State.ToString(),
                remainingMs = snapshot.RemainingMs
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error skipping break");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }
}
