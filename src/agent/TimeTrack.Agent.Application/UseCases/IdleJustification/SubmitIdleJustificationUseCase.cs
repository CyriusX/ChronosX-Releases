using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;

namespace TimeTrack.Agent.Application.UseCases.IdleJustification;

public sealed class SubmitIdleJustificationRequest
{
    public Guid IdlePeriodId { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string? Note { get; init; }
}

public sealed class SubmitIdleJustificationResponse
{
    public Guid IdlePeriodId { get; init; }
    public string ReasonCode { get; init; } = string.Empty;
    public string? Note { get; init; }
    public DateTime SubmittedAtUtc { get; init; }
}

/// <summary>
/// Stores an idle justification locally and queues it for backend sync.
/// </summary>
public sealed class SubmitIdleJustificationUseCase
{
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly ILogger<SubmitIdleJustificationUseCase> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SubmitIdleJustificationUseCase(
        IIdlePeriodRepository idlePeriodRepository,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        ILogger<SubmitIdleJustificationUseCase> logger)
    {
        _idlePeriodRepository = idlePeriodRepository;
        _idempotencyKeyGenerator = idempotencyKeyGenerator;
        _logger = logger;
    }

    public async Task<SubmitIdleJustificationResponse> ExecuteAsync(
        SubmitIdleJustificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var idlePeriod = await _idlePeriodRepository.GetByIdAsync(request.IdlePeriodId, cancellationToken)
            ?? throw new InvalidOperationException($"Idle period {request.IdlePeriodId} was not found");

        var submittedAtUtc = DateTime.UtcNow;
        idlePeriod.SubmitJustification(request.ReasonCode, request.Note, submittedAtUtc);

        var payload = new IdleJustificationSyncPayload
        {
            IdlePeriodId = idlePeriod.Id,
            ReasonCode = idlePeriod.JustificationReasonCode!,
            Note = idlePeriod.JustificationNote,
            SubmittedAtUtc = submittedAtUtc
        };

        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var idempotencyKey = _idempotencyKeyGenerator.Generate(
            "idle_justification",
            idlePeriod.Id,
            submittedAtUtc);

        var outboxItem = OutboxItem.Create(
            "idle_justification",
            idlePeriod.Id,
            payloadJson,
            idempotencyKey);

        await _idlePeriodRepository.UpdateWithOutboxAsync(idlePeriod, new[] { outboxItem }, cancellationToken);

        _logger.LogInformation("Idle justification submitted for idle period {IdlePeriodId}", idlePeriod.Id);

        return new SubmitIdleJustificationResponse
        {
            IdlePeriodId = idlePeriod.Id,
            ReasonCode = idlePeriod.JustificationReasonCode!,
            Note = idlePeriod.JustificationNote,
            SubmittedAtUtc = submittedAtUtc
        };
    }

    private sealed class IdleJustificationSyncPayload
    {
        public Guid IdlePeriodId { get; init; }
        public string ReasonCode { get; init; } = string.Empty;
        public string? Note { get; init; }
        public DateTime SubmittedAtUtc { get; init; }
    }
}
