using System.Text.Json;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.Agent.Domain.Services;
using TimeTrack.Agent.Domain.ValueObjects;

namespace TimeTrack.Agent.Application.UseCases.RecordIdlePeriod;

/// <summary>
/// Use Case para registrar períodos de inatividade
/// Responsável por criar e persistir idle periods com sincronização
/// </summary>
public sealed class RecordIdlePeriodUseCase
{
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IIdempotencyKeyGenerator _idempotencyKeyGenerator;
    private readonly ILogger<RecordIdlePeriodUseCase> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RecordIdlePeriodUseCase(
        IIdlePeriodRepository idlePeriodRepository,
        ICurrentUserContext userContext,
        IIdempotencyKeyGenerator idempotencyKeyGenerator,
        ILogger<RecordIdlePeriodUseCase> logger)
    {
        _idlePeriodRepository = idlePeriodRepository ?? throw new ArgumentNullException(nameof(idlePeriodRepository));
        _userContext = userContext ?? throw new ArgumentNullException(nameof(userContext));
        _idempotencyKeyGenerator = idempotencyKeyGenerator ?? throw new ArgumentNullException(nameof(idempotencyKeyGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executa o registro do período de inatividade
    /// </summary>
    public async Task<RecordIdlePeriodResponse> ExecuteAsync(
        RecordIdlePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = _userContext.UserId
            ?? throw new InvalidOperationException("User not authenticated");

        // Cria o período de inatividade
        var period = new TimeRange(request.StartedAt, request.EndedAt);
        var idlePeriod = IdlePeriod.Create(userId, period, request.ThresholdSeconds, request.IsSystemDetected);

        _logger.LogInformation(
            "Recording idle period: {Start} to {End} ({Duration:mm\\:ss})",
            request.StartedAt,
            request.EndedAt,
            idlePeriod.Duration);

        // Salva com outbox para sincronização
        await SaveWithOutboxAsync(idlePeriod, cancellationToken);

        return new RecordIdlePeriodResponse
        {
            IdlePeriodId = idlePeriod.Id,
            Duration = idlePeriod.Duration
        };
    }

    /// <summary>
    /// Salva o período com outbox item em uma única transação
    /// </summary>
    private async Task SaveWithOutboxAsync(IdlePeriod period, CancellationToken cancellationToken)
    {
        var payload = CreateIdlePeriodPayload(period);
        var payloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        var idempotencyKey = _idempotencyKeyGenerator.Generate(
            "idle_period",
            period.Id,
            period.Period.StartUtc);

        var outboxItem = OutboxItem.Create(
            "idle_period",
            period.Id,
            payloadJson,
            idempotencyKey);

        _logger.LogDebug(
            "Created outbox item for idle period: Id={OutboxId}, EntityId={EntityId}",
            outboxItem.Id,
            period.Id);

        await _idlePeriodRepository.SaveWithOutboxAsync(period, new[] { outboxItem }, cancellationToken);
    }

    /// <summary>
    /// Cria o payload para sincronização
    /// </summary>
    private static IdlePeriodSyncPayload CreateIdlePeriodPayload(IdlePeriod period)
    {
        return new IdlePeriodSyncPayload
        {
            Id = period.Id,
            StartedAt = period.Period.StartUtc,
            EndedAt = period.Period.EndUtc,
            ThresholdSeconds = period.ThresholdSeconds,
            IsSystemDetected = period.IsSystemDetected
        };
    }

    /// <summary>
    /// Payload de sincronização para idle_period
    /// </summary>
    private sealed class IdlePeriodSyncPayload
    {
        public Guid Id { get; init; }
        public DateTime StartedAt { get; init; }
        public DateTime EndedAt { get; init; }
        public int ThresholdSeconds { get; init; }
        public bool IsSystemDetected { get; init; }
    }
}
