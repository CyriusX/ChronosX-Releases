using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Ingest.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;

namespace TimeTrack.Backend.Application.Ingest.Commands;

/// <summary>
/// Command para ingestão de sessões de atividade
/// </summary>
public sealed class IngestActivitySessionsCommand : IRequest<IngestResponse>
{
    public required IEnumerable<ActivitySessionItem> Items { get; init; }
}

/// <summary>
/// Handler para ingestão de sessões de atividade
/// </summary>
public sealed class IngestActivitySessionsCommandHandler : IRequestHandler<IngestActivitySessionsCommand, IngestResponse>
{
    private readonly IActivitySessionRepository _activitySessionRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<IngestActivitySessionsCommandHandler> _logger;

    private const string EntityType = "ActivitySession";

    public IngestActivitySessionsCommandHandler(
        IActivitySessionRepository activitySessionRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICurrentUserContext currentUser,
        ILogger<IngestActivitySessionsCommandHandler> logger)
    {
        _activitySessionRepository = activitySessionRepository;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IngestResponse> Handle(
        IngestActivitySessionsCommand request,
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

        foreach (var item in items)
        {
            try
            {
                // Check idempotency
                var exists = await _idempotencyKeyRepository.ExistsAsync(
                    item.IdempotencyKey,
                    EntityType,
                    cancellationToken);

                if (exists)
                {
                    duplicates++;
                    _logger.LogDebug("Duplicate activity session ignored: {IdempotencyKey}", item.IdempotencyKey);
                    continue;
                }

                // Create entity
                var session = ActivitySession.Create(
                    item.Id,
                    orgId,
                    deviceIdClaim.Value,
                    userId,
                    item.ProcessName,
                    item.WindowTitle,
                    item.AppCategory,
                    item.StartedAt,
                    item.EndedAt,
                    item.IdempotencyKey);

                // Create idempotency key record
                var idempotencyKey = IdempotencyKey.Create(
                    orgId,
                    item.IdempotencyKey,
                    EntityType,
                    item.Id);

                // Save both in same transaction (handled by repository SaveChangesAsync)
                await _activitySessionRepository.AddAsync(session, cancellationToken);
                await _idempotencyKeyRepository.AddAsync(idempotencyKey, cancellationToken);

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing activity session item {ItemId}", item.Id);
                errors.Add(new IngestError
                {
                    ItemId = item.Id,
                    Code = "PROCESSING_ERROR",
                    Message = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Ingested activity sessions: {Processed} processed, {Duplicates} duplicates, {Errors} errors",
            processed, duplicates, errors.Count);

        return new IngestResponse
        {
            Processed = processed,
            Duplicates = duplicates,
            Errors = errors
        };
    }
}
