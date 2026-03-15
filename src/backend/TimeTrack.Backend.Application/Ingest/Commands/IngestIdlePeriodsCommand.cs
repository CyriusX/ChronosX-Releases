using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Ingest.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Ingest.Commands;

/// <summary>
/// Command para ingestão de períodos de inatividade
/// </summary>
public sealed class IngestIdlePeriodsCommand : IRequest<IngestResponse>
{
    public required IEnumerable<IdlePeriodItem> Items { get; init; }
}

/// <summary>
/// Handler para ingestão de períodos de inatividade
/// </summary>
public sealed class IngestIdlePeriodsCommandHandler : IRequestHandler<IngestIdlePeriodsCommand, IngestResponse>
{
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<IngestIdlePeriodsCommandHandler> _logger;

    private const string EntityType = "IdlePeriod";

    public IngestIdlePeriodsCommandHandler(
        IIdlePeriodRepository idlePeriodRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICurrentUserContext currentUser,
        ILogger<IngestIdlePeriodsCommandHandler> logger)
    {
        _idlePeriodRepository = idlePeriodRepository;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IngestResponse> Handle(
        IngestIdlePeriodsCommand request,
        CancellationToken cancellationToken)
    {
        var items = request.Items.ToList();
        var processed = 0;
        var duplicates = 0;
        var errors = new List<IngestError>();

        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User context not available");
        }

        var orgId = _currentUser.OrgId.Value;
        var userId = _currentUser.UserId.Value;

        // Get device_id from claims (set during JWT validation)
        var deviceIdClaim = _currentUser.DeviceId;
        if (!deviceIdClaim.HasValue)
        {
            throw new UnauthorizedAccessException("Device ID not found in token");
        }

        // Batch check idempotency keys - single query instead of N queries
        var keysToCheck = items.Select(i => i.IdempotencyKey).Distinct();
        var existingKeys = await _idempotencyKeyRepository.GetExistingKeysAsync(
            keysToCheck,
            EntityType,
            cancellationToken);

        foreach (var item in items)
        {
            try
            {
                // Check idempotency (in-memory check)
                if (existingKeys.Contains(item.IdempotencyKey))
                {
                    duplicates++;
                    _logger.LogDebug("Duplicate idle period ignored: {IdempotencyKey}", item.IdempotencyKey);
                    continue;
                }

                // Create entity
                var idlePeriod = IdlePeriod.Create(
                    item.Id,
                    orgId,
                    deviceIdClaim.Value,
                    userId,
                    item.StartedAt,
                    item.EndedAt,
                    item.IdempotencyKey);

                // Create idempotency key record
                var idempotencyKey = IdempotencyKey.Create(
                    orgId,
                    item.IdempotencyKey,
                    EntityType,
                    item.Id);

                // Save both
                await _idlePeriodRepository.AddAsync(idlePeriod, cancellationToken);
                await _idempotencyKeyRepository.AddAsync(idempotencyKey, cancellationToken);

                // Add to existing set to prevent duplicates within same batch
                existingKeys.Add(item.IdempotencyKey);

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing idle period item {ItemId}", item.Id);
                errors.Add(new IngestError
                {
                    ItemId = item.Id,
                    Code = "PROCESSING_ERROR",
                    Message = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Ingested idle periods: {Processed} processed, {Duplicates} duplicates, {Errors} errors",
            processed, duplicates, errors.Count);

        return new IngestResponse
        {
            Processed = processed,
            Duplicates = duplicates,
            Errors = errors
        };
    }
}
