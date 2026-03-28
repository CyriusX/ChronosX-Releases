using Microsoft.Extensions.Logging;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.Tracking;

/// <summary>
/// TEMPORARY: Test handler to trigger the activity resume toast on demand.
/// Remove after testing.
/// </summary>
public sealed class TestActivityResumeToastCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "testActivityResumeToast";

    private readonly IIpcServer _ipcServer;
    private readonly ILogger<TestActivityResumeToastCommandHandler> _logger;

    public TestActivityResumeToastCommandHandler(
        IIpcServer ipcServer,
        ILogger<TestActivityResumeToastCommandHandler> logger)
    {
        _ipcServer = ipcServer;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        _logger.LogInformation("TEST: Triggering activity resume toast");
        await _ipcServer.SendEventAsync(new IpcEvent
        {
            EventType = "showActivityResumePrompt",
            Payload = new { countdownSeconds = 20 }
        }, ct);
        return SuccessResponse(request.RequestId, new { triggered = true });
    }
}
