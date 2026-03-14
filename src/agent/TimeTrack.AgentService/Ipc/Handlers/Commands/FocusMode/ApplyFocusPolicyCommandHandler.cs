using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Application.FocusMode;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Enums;
using TimeTrack.Agent.Domain.ValueObjects;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Handles applying focus mode policy from the frontend
///
/// Uses manual JSON parsing because FocusModePolicy is a ValueObject
/// with a parameterized constructor that System.Text.Json cannot deserialize directly.
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
            _logger.LogInformation("ApplyFocusPolicy command received");

            if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
            {
                _logger.LogWarning("Invalid payload: no object provided");
                return ErrorResponse(request.RequestId, "Invalid focus mode policy payload");
            }

            var payload = request.Payload.Value;
            var policy = ParsePolicy(payload);

            if (policy is null)
            {
                _logger.LogWarning("Failed to parse policy from payload");
                return ErrorResponse(request.RequestId, "Failed to parse focus mode policy");
            }

            _logger.LogInformation("Applying policy: Enabled={Enabled}, Mode={Mode}, AllowUserOverride={AllowUserOverride}",
                policy.Enabled, policy.Mode, policy.AllowUserOverride);

            _focusModeEngine.ApplyPolicy(policy);
            var snapshot = _focusModeEngine.GetSnapshot();

            _logger.LogInformation("Policy applied successfully. Current state: {State}, Mode: {Mode}",
                snapshot.State, snapshot.Mode);

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

    /// <summary>
    /// Parses the JSON payload into a FocusModePolicy value object
    /// </summary>
    private FocusModePolicy? ParsePolicy(JsonElement payload)
    {
        // Get enabled flag
        bool enabled = false;
        if (payload.TryGetProperty("enabled", out var enabledProp))
        {
            enabled = enabledProp.ValueKind == JsonValueKind.True;
        }

        // Get mode
        FocusModeType mode = FocusModeType.None;
        if (payload.TryGetProperty("mode", out var modeProp) && modeProp.ValueKind == JsonValueKind.String)
        {
            var modeStr = modeProp.GetString();
            mode = modeStr?.ToLowerInvariant() switch
            {
                "pomodoro" => FocusModeType.Pomodoro,
                "ultradian" => FocusModeType.Ultradian,
                "none" => FocusModeType.None,
                _ => FocusModeType.None
            };
        }

        // Get allowUserOverride
        bool allowUserOverride = true;
        if (payload.TryGetProperty("allowUserOverride", out var overrideProp))
        {
            allowUserOverride = overrideProp.ValueKind != JsonValueKind.False;
        }

        _logger.LogDebug("Parsed policy values: enabled={Enabled}, mode={Mode}, allowUserOverride={AllowUserOverride}",
            enabled, mode, allowUserOverride);

        // If disabled or none, return disabled policy
        if (!enabled || mode == FocusModeType.None)
        {
            return FocusModePolicy.Disabled;
        }

        // Build the appropriate policy based on mode
        if (mode == FocusModeType.Pomodoro)
        {
            var pomodoroConfig = ParsePomodoroConfig(payload);
            return new FocusModePolicy(enabled, mode, allowUserOverride, pomodoroConfig, null);
        }
        else if (mode == FocusModeType.Ultradian)
        {
            var ultradianConfig = ParseUltradianConfig(payload);
            return new FocusModePolicy(enabled, mode, allowUserOverride, null, ultradianConfig);
        }

        return FocusModePolicy.Disabled;
    }

    /// <summary>
    /// Parses Pomodoro configuration from the payload
    /// </summary>
    private PomodoroConfig? ParsePomodoroConfig(JsonElement payload)
    {
        if (!payload.TryGetProperty("pomodoro", out var pomodoroProp) ||
            pomodoroProp.ValueKind != JsonValueKind.Object)
        {
            _logger.LogDebug("No pomodoro config found, using defaults");
            return PomodoroConfig.Default;
        }

        int focusMinutes = GetIntOrDefault(pomodoroProp, "focusMinutes", 25);
        int shortBreakMinutes = GetIntOrDefault(pomodoroProp, "shortBreakMinutes", 5);
        int longBreakMinutes = GetIntOrDefault(pomodoroProp, "longBreakMinutes", 15);
        int cyclesBeforeLongBreak = GetIntOrDefault(pomodoroProp, "cyclesBeforeLongBreak", 4);

        try
        {
            return new PomodoroConfig(focusMinutes, shortBreakMinutes, longBreakMinutes, cyclesBeforeLongBreak);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid pomodoro config values, using defaults");
            return PomodoroConfig.Default;
        }
    }

    /// <summary>
    /// Parses Ultradian configuration from the payload
    /// </summary>
    private UltradianConfig? ParseUltradianConfig(JsonElement payload)
    {
        if (!payload.TryGetProperty("ultradian", out var ultradianProp) ||
            ultradianProp.ValueKind != JsonValueKind.Object)
        {
            _logger.LogDebug("No ultradian config found, using defaults");
            return UltradianConfig.Default;
        }

        int focusMinutes = GetIntOrDefault(ultradianProp, "focusMinutes", 90);
        int breakMinutes = GetIntOrDefault(ultradianProp, "breakMinutes", 20);

        try
        {
            return new UltradianConfig(focusMinutes, breakMinutes);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning(ex, "Invalid ultradian config values, using defaults");
            return UltradianConfig.Default;
        }
    }

    /// <summary>
    /// Gets an integer property from a JSON element, with a default fallback
    /// </summary>
    private static int GetIntOrDefault(JsonElement element, string propertyName, int defaultValue)
    {
        if (element.TryGetProperty(propertyName, out var prop) &&
            prop.ValueKind == JsonValueKind.Number)
        {
            return prop.GetInt32();
        }
        return defaultValue;
    }
}
