using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Polls backend for pending remote commands and executes them.
/// Called each SyncWorker cycle (~60s).
/// </summary>
public sealed class RemoteCommandService : IRemoteCommandService
{
    private readonly HttpClient _httpClient;
    private readonly ITokenStore _tokenStore;
    private readonly ICurrentUserContext _userContext;
    private readonly IAgentEventLogger _eventLogger;
    private readonly ILogger<RemoteCommandService> _logger;
    private readonly Func<IRemoteCommandExecutor> _executorFactory;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RemoteCommandService(
        HttpClient httpClient,
        ITokenStore tokenStore,
        ICurrentUserContext userContext,
        IAgentEventLogger eventLogger,
        Func<IRemoteCommandExecutor> executorFactory,
        ILogger<RemoteCommandService> logger)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
        _userContext = userContext;
        _eventLogger = eventLogger;
        _executorFactory = executorFactory;
        _logger = logger;
    }

    public async Task PollAndExecuteAsync(CancellationToken cancellationToken = default)
    {
        if (!_userContext.IsAuthenticated || !_userContext.DeviceId.HasValue)
            return;

        var deviceId = _userContext.DeviceId.Value;

        try
        {
            // Ensure valid token
            if (_tokenStore.IsJwtExpiringSoon(2))
                await _tokenStore.RefreshAsync(cancellationToken);

            var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
            if (string.IsNullOrEmpty(jwt)) return;

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", jwt);

            // Poll for pending commands
            var response = await _httpClient.GetAsync(
                $"/api/v1/devices/{deviceId}/pending-commands",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Pending commands poll returned {Status}", response.StatusCode);
                return;
            }

            var pendingResponse = await response.Content.ReadFromJsonAsync<PendingCommandsResponse>(
                JsonOptions, cancellationToken);

            if (pendingResponse?.Commands == null || pendingResponse.Commands.Count == 0)
                return;

            _logger.LogInformation("Received {Count} pending remote command(s)", pendingResponse.Commands.Count);

            var executor = _executorFactory();

            foreach (var cmd in pendingResponse.Commands)
            {
                await ExecuteAndAcknowledgeAsync(deviceId, cmd, executor, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remote command poll/execute failed");
        }
    }

    private async Task ExecuteAndAcknowledgeAsync(
        Guid deviceId,
        PendingCommandItem cmd,
        IRemoteCommandExecutor executor,
        CancellationToken cancellationToken)
    {
        string ackStatus;
        string? resultJson = null;

        try
        {
            _logger.LogInformation("Executing remote command {CommandId}: {Type}", cmd.Id, cmd.CommandType);

            await _eventLogger.LogAsync(
                $"remote.{cmd.CommandType}", AgentEventCategory.System, AgentEventSeverity.Info,
                $"Executando comando remoto: {cmd.CommandType}",
                new { commandId = cmd.Id, commandType = cmd.CommandType },
                cancellationToken);

            var result = await executor.ExecuteAsync(cmd.CommandType, cmd.PayloadJson, cancellationToken);
            ackStatus = result.Success ? "completed" : "failed";
            resultJson = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Remote command {CommandId} execution failed", cmd.Id);
            ackStatus = "failed";
            resultJson = ex.Message;
        }

        // Acknowledge back to backend
        try
        {
            var ackPayload = new { status = ackStatus, resultJson };
            await _httpClient.PostAsJsonAsync(
                $"/api/v1/devices/{deviceId}/commands/{cmd.Id}/ack",
                ackPayload,
                cancellationToken);

            _logger.LogInformation("Command {CommandId} acknowledged as {Status}", cmd.Id, ackStatus);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to acknowledge command {CommandId}", cmd.Id);
        }
    }

    // DTOs for deserialization
    private sealed class PendingCommandsResponse
    {
        public List<PendingCommandItem> Commands { get; set; } = [];
    }

    private sealed class PendingCommandItem
    {
        public Guid Id { get; set; }
        public string CommandType { get; set; } = string.Empty;
        public string? PayloadJson { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

/// <summary>
/// Interface for the command executor (resolved from DI)
/// </summary>
public interface IRemoteCommandExecutor
{
    Task<CommandResult> ExecuteAsync(string commandType, string? payloadJson, CancellationToken ct);
}

public sealed class CommandResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }

    public static CommandResult Ok(string? message = null) => new() { Success = true, Message = message };
    public static CommandResult Failed(string message) => new() { Success = false, Message = message };
}
