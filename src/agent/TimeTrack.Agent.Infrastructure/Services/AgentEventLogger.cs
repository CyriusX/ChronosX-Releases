using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;

namespace TimeTrack.Agent.Infrastructure.Services;

/// <summary>
/// Implementação do logger de eventos do agent.
/// Persiste eventos localmente em SQLite e cria OutboxItems para sync com o backend.
/// Fire-and-forget: erros de logging nunca propagam para o chamador.
/// </summary>
public sealed class AgentEventLogger : IAgentEventLogger
{
    private readonly IAgentEventLogRepository _eventLogRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly ILogger<AgentEventLogger> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AgentEventLogger(
        IAgentEventLogRepository eventLogRepository,
        IOutboxRepository outboxRepository,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        ILogger<AgentEventLogger> logger)
    {
        _eventLogRepository = eventLogRepository;
        _outboxRepository = outboxRepository;
        _idempotencyKeyGenerator = idempotencyKeyGenerator;
        _logger = logger;
    }

    public async Task LogAsync(
        string eventType,
        string category,
        string severity,
        string message,
        object? metadata = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var metadataJson = metadata != null
                ? JsonSerializer.Serialize(metadata, JsonOptions)
                : null;

            var eventLog = AgentEventLog.Create(eventType, category, severity, message, metadataJson);

            // Persist locally
            await _eventLogRepository.AddAsync(eventLog, cancellationToken);

            // Create outbox item for sync
            var payload = new
            {
                eventLog.Id,
                eventLog.EventType,
                eventLog.Category,
                eventLog.Severity,
                eventLog.Message,
                eventLog.MetadataJson,
                Timestamp = eventLog.TimestampUtc
            };

            var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
            var idempotencyKey = _idempotencyKeyGenerator.Generate(
                "agent_event", eventLog.Id, eventLog.TimestampUtc);

            var outboxItem = OutboxItem.Create(
                "agent_event",
                eventLog.Id,
                payloadJson,
                idempotencyKey);

            await _outboxRepository.AddAsync(outboxItem, cancellationToken);
        }
        catch (Exception ex)
        {
            // Fire-and-forget: never propagate logging errors
            _logger.LogDebug(ex, "Failed to log agent event: {EventType}", eventType);
        }
    }
}
