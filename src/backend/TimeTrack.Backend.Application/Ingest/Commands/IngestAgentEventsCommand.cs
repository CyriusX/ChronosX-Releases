using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Ingest.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Ingest.Commands;

public sealed class IngestAgentEventsCommand : IRequest<IngestResponse>
{
    public required IEnumerable<AgentEventItem> Items { get; init; }
}

public sealed class IngestAgentEventsCommandHandler : IRequestHandler<IngestAgentEventsCommand, IngestResponse>
{
    private readonly IAgentEventLogRepository _eventLogRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IOpsMcpPushNotifier _pushNotifier;
    private readonly ILogger<IngestAgentEventsCommandHandler> _logger;

    private const string EntityType = "AgentEvent";

    public IngestAgentEventsCommandHandler(
        IAgentEventLogRepository eventLogRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICurrentUserContext currentUser,
        IOpsMcpPushNotifier pushNotifier,
        ILogger<IngestAgentEventsCommandHandler> logger)
    {
        _eventLogRepository = eventLogRepository;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _currentUser = currentUser;
        _pushNotifier = pushNotifier;
        _logger = logger;
    }

    public async Task<IngestResponse> Handle(
        IngestAgentEventsCommand request,
        CancellationToken cancellationToken)
    {
        var items = request.Items.ToList();
        var processed = 0;
        var duplicates = 0;
        var errors = new List<IngestError>();
        var newCriticalCount = 0;

        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User context not available");

        var orgId = _currentUser.OrgId.Value;
        var deviceIdClaim = _currentUser.DeviceId;
        if (!deviceIdClaim.HasValue)
            throw new UnauthorizedAccessException("Device ID not found in token");

        var keysToCheck = items.Select(i => i.IdempotencyKey).Distinct();
        var existingKeys = await _idempotencyKeyRepository.GetExistingKeysAsync(
            keysToCheck, EntityType, cancellationToken);

        foreach (var item in items)
        {
            try
            {
                if (existingKeys.Contains(item.IdempotencyKey))
                {
                    duplicates++;
                    continue;
                }

                var eventLog = AgentEventLog.Create(
                    item.Id,
                    orgId,
                    deviceIdClaim.Value,
                    item.EventType,
                    item.Category,
                    item.Severity,
                    item.Message,
                    item.MetadataJson,
                    item.Timestamp,
                    item.IdempotencyKey);

                var idempotencyKey = IdempotencyKey.Create(
                    orgId, item.IdempotencyKey, EntityType, item.Id);

                await _eventLogRepository.AddAsync(eventLog, cancellationToken);
                await _idempotencyKeyRepository.AddAsync(idempotencyKey, cancellationToken);

                existingKeys.Add(item.IdempotencyKey);
                processed++;

                if (string.Equals(item.Severity, "critical", StringComparison.OrdinalIgnoreCase))
                {
                    newCriticalCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing agent event item {ItemId}", item.Id);
                errors.Add(new IngestError
                {
                    ItemId = item.Id,
                    Code = "PROCESSING_ERROR",
                    Message = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Ingested agent events: {Processed} processed, {Duplicates} duplicates, {Errors} errors",
            processed, duplicates, errors.Count);

        if (newCriticalCount > 0)
        {
            try
            {
                await _pushNotifier.NotifyResourceUpdatedAsync("ops://critical", cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed pushing MCP notification for ops://critical");
            }
        }

        return new IngestResponse
        {
            Processed = processed,
            Duplicates = duplicates,
            Errors = errors
        };
    }
}
