using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.AgentService.Ipc.Handlers;

namespace TimeTrack.AgentService.Ipc.Handlers.Commands.FocusMode;

/// <summary>
/// Records a completed focus session from the frontend timer (Pomodoro/Ultradian).
/// Creates an OutboxItem so the SyncWorker sends it to the cloud DB (Neon).
/// </summary>
public sealed class RecordFocusSessionCommandHandler : IpcHandlerBase, IIpcCommandHandler
{
    public string CommandName => "RecordFocusSession";

    private readonly IOutboxRepository _outboxRepository;
    private readonly ILogger<RecordFocusSessionCommandHandler> _logger;

    public RecordFocusSessionCommandHandler(
        IOutboxRepository outboxRepository,
        ILogger<RecordFocusSessionCommandHandler> logger)
    {
        _outboxRepository = outboxRepository;
        _logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken ct)
    {
        if (!request.Payload.HasValue || request.Payload.Value.ValueKind != JsonValueKind.Object)
        {
            return ErrorResponse(request.RequestId, "Missing session payload");
        }

        try
        {
            var payload = request.Payload.Value;

            var sessionId = payload.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                ? Guid.TryParse(idProp.GetString(), out var parsed) ? parsed : Guid.NewGuid()
                : Guid.NewGuid();

            var startedAt = payload.TryGetProperty("startedAt", out var startProp)
                ? DateTime.TryParse(startProp.GetString(), out var s) ? s.ToUniversalTime() : DateTime.UtcNow
                : DateTime.UtcNow;

            var completedAt = payload.TryGetProperty("completedAt", out var endProp)
                ? DateTime.TryParse(endProp.GetString(), out var e) ? e.ToUniversalTime() : DateTime.UtcNow
                : DateTime.UtcNow;

            var durationMs = payload.TryGetProperty("durationMs", out var durProp) ? durProp.GetInt64() : 0L;
            var plannedMinutes = (int)(durationMs / 60000);
            var actualMinutes = (int)(durationMs / 60000);

            var mode = payload.TryGetProperty("mode", out var modeProp) ? modeProp.GetString() ?? "pomodoro" : "pomodoro";
            var name = payload.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
            var productivity = payload.TryGetProperty("productivity", out var prodProp) ? prodProp.GetInt32() : 0;

            var status = durationMs >= 5 * 60000 ? "Completed" : "Short";

            var outboxPayload = new
            {
                Id = sessionId,
                StartedAt = startedAt,
                EndedAt = completedAt,
                PlannedDurationMinutes = plannedMinutes,
                ActualDurationMinutes = actualMinutes,
                Status = status,
                FocusScore = productivity >= 0 ? productivity : (int?)null,
                Mode = mode,
                Name = name
            };

            var payloadJson = JsonSerializer.Serialize(outboxPayload);
            var idempotencyKey = GenerateIdempotencyKey(sessionId, startedAt);

            var outboxItem = OutboxItem.Create(
                "focus_session",
                sessionId,
                payloadJson,
                idempotencyKey);

            await _outboxRepository.AddAsync(outboxItem, ct);

            _logger.LogInformation(
                "Focus session recorded and queued for sync: {SessionId}, Mode: {Mode}, Duration: {Duration}ms, Score: {Score}",
                sessionId, mode, durationMs, productivity);

            return SuccessResponse(request.RequestId, new { recorded = true, sessionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording focus session");
            return UnknownErrorResponse(request.RequestId, ex);
        }
    }

    private static string GenerateIdempotencyKey(Guid sessionId, DateTime startedAt)
    {
        var input = $"{sessionId}:{startedAt:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..32];
    }
}
