using MediatR;
using Microsoft.Extensions.Logging;
using TimeTrack.Backend.Application.Common.Interfaces;
using TimeTrack.Backend.Application.Ingest.DTOs;
using TimeTrack.Backend.Domain.Entities;
using TimeTrack.Backend.Domain.Interfaces.Repositories;
using TimeTrack.Backend.Domain.ValueObjects;

namespace TimeTrack.Backend.Application.Ingest.Commands;

/// <summary>
/// Command para ingestão de sessões de foco
/// </summary>
public sealed class IngestFocusSessionsCommand : IRequest<IngestResponse>
{
    public required IEnumerable<FocusSessionItem> Items { get; init; }
}

/// <summary>
/// Handler para ingestão de sessões de foco
/// </summary>
public sealed class IngestFocusSessionsCommandHandler : IRequestHandler<IngestFocusSessionsCommand, IngestResponse>
{
    private readonly IFocusSessionRepository _focusSessionRepository;
    private readonly IIdempotencyKeyRepository _idempotencyKeyRepository;
    private readonly ICurrentUserContext _currentUser;
    private readonly ILogger<IngestFocusSessionsCommandHandler> _logger;

    private const string EntityType = "FocusSession";

    public IngestFocusSessionsCommandHandler(
        IFocusSessionRepository focusSessionRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ICurrentUserContext currentUser,
        ILogger<IngestFocusSessionsCommandHandler> logger)
    {
        _focusSessionRepository = focusSessionRepository;
        _idempotencyKeyRepository = idempotencyKeyRepository;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<IngestResponse> Handle(
        IngestFocusSessionsCommand request,
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
                    _logger.LogDebug("Duplicate focus session ignored: {IdempotencyKey}", item.IdempotencyKey);
                    continue;
                }

                // Parse status
                var status = ParseStatus(item.Status);

                // Create entity
                var session = FocusSession.Create(
                    item.Id,
                    orgId,
                    deviceIdClaim.Value,
                    userId,
                    item.StartedAt,
                    item.PlannedDurationMinutes,
                    item.IdempotencyKey);

                // Update status based on what was sent
                if (status == FocusSessionStatus.Completed && item.EndedAt.HasValue && item.FocusScore.HasValue)
                {
                    session.Complete(item.EndedAt.Value, item.FocusScore.Value);
                }
                else if (status == FocusSessionStatus.Cancelled && item.EndedAt.HasValue)
                {
                    session.Cancel(item.EndedAt.Value);
                }

                // Create idempotency key record
                var idempotencyKey = IdempotencyKey.Create(
                    orgId,
                    item.IdempotencyKey,
                    EntityType,
                    item.Id);

                // Save both in same transaction (handled by repository SaveChangesAsync)
                await _focusSessionRepository.AddAsync(session, cancellationToken);
                await _idempotencyKeyRepository.AddAsync(idempotencyKey, cancellationToken);

                // Add to existing set to prevent duplicates within same batch
                existingKeys.Add(item.IdempotencyKey);

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing focus session item {ItemId}", item.Id);
                errors.Add(new IngestError
                {
                    ItemId = item.Id,
                    Code = "PROCESSING_ERROR",
                    Message = ex.Message
                });
            }
        }

        _logger.LogInformation(
            "Ingested focus sessions: {Processed} processed, {Duplicates} duplicates, {Errors} errors",
            processed, duplicates, errors.Count);

        return new IngestResponse
        {
            Processed = processed,
            Duplicates = duplicates,
            Errors = errors
        };
    }

    private static FocusSessionStatus ParseStatus(string status)
    {
        return status?.ToLowerInvariant() switch
        {
            "completed" => FocusSessionStatus.Completed,
            "cancelled" => FocusSessionStatus.Cancelled,
            "inprogress" or "in_progress" => FocusSessionStatus.InProgress,
            _ => FocusSessionStatus.InProgress
        };
    }
}
