using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles starting focus mode
/// </summary>
public sealed class StartFocusModeCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "StartFocusMode";

    private readonly IFocusModeEngine _focusModeEngine;
    private readonly ILogger<StartFocusModeCommandHandler> _logger;

    public StartFocusModeCommandHandler(
        IFocusModeEngine focusModeEngine,
        ILogger<StartFocusModeCommandHandler> logger)
    {
        _focusModeEngine = focusModeEngine;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        try
        {
        _logger.LogInformation("StartFocusMode command received");

        // Check if we need to apply a default policy first
        var currentPolicy = _focusModeEngine.GetPolicy();
        _logger.LogInformation("Current policy: Enabled={Enabled}, Mode={Mode}",
            currentPolicy.Enabled, currentPolicy.Mode);

        if (currentPolicy is { Enabled: false } or { Mode: FocusModeType.None })
        {
            _logger.LogInformation("No policy applied, using default Pomodoro policy");

            // Start with default Pomodoro policy
            var defaultPolicy = FocusModePolicy.DefaultPomodoro;

            // Try to get mode from request payload
            if (request.Payload.HasValue)
            {
                try
                {
                    var payloadElement = request.Payload.Value;
                    if (payloadElement.ValueKind == JsonValueKind.Object &&
                        payloadElement.TryGetProperty("mode", out var modeProp) &&
                        modeProp.ValueKind == JsonValueKind.String)
                    {
                        var modeStr = modeProp.GetString();
                        if (Enum.TryParse<FocusModeType>(modeStr, ignoreCase: true, out var parsedMode))
                        {
                            if (parsedMode == FocusModeType.Pomodoro)
                            {
                                defaultPolicy = FocusModePolicy.DefaultPomodoro;
                            }
                            else if (parsedMode == FocusModeType.Ultradian)
                            {
                                defaultPolicy = FocusModePolicy.DefaultUltradian;
                            }
                            _logger.LogInformation("Using mode from payload: {Mode}", parsedMode);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse mode from payload, using default");
                }
            }

            _focusModeEngine.ApplyPolicy(defaultPolicy);
            _logger.LogInformation("Default policy applied");
        }

        var success = _focusModeEngine.Start();
        var snapshot = _focusModeEngine.GetSnapshot();

        _logger.LogInformation("Start result: Success={Success}, State={State}",
            success, snapshot.State);

        return SuccessResponse(request.RequestId, new
        {
            started = success,
            state = snapshot.State.ToString(),
            remainingMs = snapshot.RemainingMs
        });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error starting focus mode");
        return UnknownErrorResponse(request.RequestId, ex);
    }
    }
}
