using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Ingest.DTOs;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Ingest.Commands;

/// <summary>
/// Command para ingestão de justificativas de períodos de inatividade
/// </summary>
public sealed class IngestIdleJustificationsCommand : IRequest<IngestResponse>
{
    public required IEnumerable<IdleJustificationItem> Items { get; init; }
}

/// <summary>
/// Handler para ingestão de justificativas de períodos de inatividade
/// </summary>
public sealed class IngestIdleJustificationsCommandHandler : IRequestHandler<IngestIdleJustificationsCommand, IngestResponse>
{
    private const string EntityType = "IdleJustification";

    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<IngestIdleJustificationsCommandHandler> _logger;

    public IngestIdleJustificationsCommandHandler(
        IIdlePeriodRepository idlePeriodRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICurrentUserContext currentUser,
        ILogger<IngestIdleJustificationsCommandHandler> logger)
    {
        _idlePeriodRepository = idlePeriodRepository;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IngestResponse> Handle(
        IngestIdleJustificationsCommand request,
        CancellationToken cancellationToken)
    {
        var items = request.Items.ToList();
        var processed = 0;
        var duplicates = 0;
        var errors = new List<IngestError>();

        if (!_currentUser.OrgId.HasValue || !_currentUser.UserId.HasValue)
            throw new UnauthorizedAccessException("User context not available");

        var keysToCheck = items.Select(i => i.IdempotencyKey).Distinct();
        var existingKeys = await _idempotencyKeyRepository.GetExistingKeysAsync(
            keysToCheck,
            EntityType,
            cancellationToken);

        foreach (var item in items)
        {
            try
            {
                if (existingKeys.Contains(item.IdempotencyKey))
                {
                    duplicates++;
                    continue;
                }

                var idlePeriod = await _idlePeriodRepository.GetByIdAsync(item.IdlePeriodId, cancellationToken);
                if (idlePeriod is null)
                {
                    errors.Add(new IngestError
                    {
                        ItemId = item.IdlePeriodId,
                        Code = "IDLE_PERIOD_NOT_FOUND",
                        Message = $"Idle period {item.IdlePeriodId} was not found"
                    });
                    continue;
                }

                idlePeriod.SubmitJustification(item.ReasonCode, item.Note, item.SubmittedAtUtc);
                await _idlePeriodRepository.UpdateAsync(idlePeriod, cancellationToken);

                var idempotencyKey = Domain.Entities.IdempotencyKey.Create(
                    _currentUser.OrgId.Value,
                    item.IdempotencyKey,
                    EntityType,
                    item.IdlePeriodId);

                await _idempotencyKeyRepository.AddAsync(idempotencyKey, cancellationToken);
                existingKeys.Add(item.IdempotencyKey);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing idle justification for idle period {IdlePeriodId}", item.IdlePeriodId);
                errors.Add(new IngestError
                {
                    ItemId = item.IdlePeriodId,
                    Code = "PROCESSING_ERROR",
                    Message = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Ingested idle justifications: {Processed} processed, {Duplicates} duplicates, {Errors} errors",
            processed, duplicates, errors.Count);

        return new IngestResponse
        {
            Processed = processed,
            Duplicates = duplicates,
            Errors = errors
        };
    }
}
