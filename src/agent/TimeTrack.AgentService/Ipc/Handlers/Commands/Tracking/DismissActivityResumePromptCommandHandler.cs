using Microsoft.Extensions.Logging;
using TimeTrack.AgentService.Workers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// Handles the "DismissActivityResumePrompt" command sent when the user
/// clicks "No" on the activity resume toast. Suppresses further prompts
/// for the current pause session.
/// </summary>
public sealed class DismissActivityResumePromptCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "dismissActivityResumePrompt";

    private readonly ActivityResumeState _resumeState;
    private readonly ILogger<DismissActivityResumePromptCommandHandler> _logger;

    public DismissActivityResumePromptCommandHandler(
        ActivityResumeState resumeState,
        ILogger<DismissActivityResumePromptCommandHandler> logger)
    {
        _resumeState = resumeState;
        _logger = logger;
    }

    public Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("Activity resume prompt dismissed by user — suppressing further prompts for this pause");
        _resumeState.Dismiss();
        return Task.FromResult(SuccessResponse(request.RequestId, new { dismissed = true }));
    }
}
