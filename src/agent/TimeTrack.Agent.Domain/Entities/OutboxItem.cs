using TimeTrack.Agent.Domain.Common;

namespace TimeTrack.Agent.Domain.Entities;

/// <summary>
/// Representa um item na fila de sincronização (Outbox Pattern)
/// </summary>
public sealed class OutboxItem : EntityBase
{
    /// <summary>
    /// Tipo da entidade (activity_session, idle_period)
    /// </summary>
    public string EntityType { get; }

    /// <summary>
    /// ID da entidade referenciada
    /// </summary>
    public Guid EntityId { get; }

    /// <summary>
    /// Snapshot JSON do payload a ser enviado
    /// </summary>
    public string PayloadJson { get; }

    /// <summary>
    /// Chave de idempotência determinística (SHA-256)
    /// </summary>
    public string IdempotencyKey { get; }

    /// <summary>
    /// Contador de tentativas de sync
    /// </summary>
    public int AttemptCount { get; private set; }

    /// <summary>
    /// Quando pode tentar novamente (backoff)
    /// </summary>
    public DateTime? NextAttemptUtc { get; private set; }

    /// <summary>
    /// Data de confirmação do servidor
    /// </summary>
    public DateTime? SentAt { get; private set; }

    /// <summary>
    /// Data de criação do item
    /// </summary>
    public DateTime CreatedAt { get; }

    /// <summary>
    /// Última mensagem de erro (se houver)
    /// </summary>
    public string? LastError { get; private set; }

    private OutboxItem() { }

    public OutboxItem(
        Guid id,
        string entityType,
        Guid entityId,
        string payloadJson,
        string idempotencyKey)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(entityType))
            throw new ArgumentException("Entity type is required", nameof(entityType));

        if (string.IsNullOrWhiteSpace(payloadJson))
            throw new ArgumentException("Payload JSON is required", nameof(payloadJson));

        if (string.IsNullOrWhiteSpace(idempotencyKey))
            throw new ArgumentException("Idempotency key is required", nameof(idempotencyKey));

        EntityType = entityType;
        EntityId = entityId;
        PayloadJson = payloadJson;
        IdempotencyKey = idempotencyKey;
        AttemptCount = 0;
        NextAttemptUtc = DateTime.UtcNow; // Pronto para envio imediato
        CreatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Cria um novo item de outbox
    /// </summary>
    public static OutboxItem Create(
        string entityType,
        Guid entityId,
        string payloadJson,
        string idempotencyKey)
    {
        return new OutboxItem(
            Guid.NewGuid(),
            entityType,
            entityId,
            payloadJson,
            idempotencyKey);
    }

    /// <summary>
    /// Marca o item como enviado com sucesso
    /// </summary>
    public void MarkAsSent()
    {
        SentAt = DateTime.UtcNow;
        LastError = null;
    }

    /// <summary>
    /// Marca o item como falhado e agenda próxima tentativa com backoff exponencial
    /// </summary>
    public void MarkAsFailed(string error)
    {
        AttemptCount++;
        LastError = error;

        // Backoff exponencial: 1min, 2min, 4min, 8min, 16min, max 30min
        var delayMinutes = Math.Min(Math.Pow(2, AttemptCount), 30);
        NextAttemptUtc = DateTime.UtcNow.AddMinutes(delayMinutes);
    }

    /// <summary>
    /// Verifica se o item está pendente de envio
    /// </summary>
    public bool IsPending => SentAt == null;

    /// <summary>
    /// Verifica se o item está pronto para tentativa de envio
    /// </summary>
    public bool IsReadyForRetry => IsPending && (NextAttemptUtc == null || NextAttemptUtc <= DateTime.UtcNow);

    public override string ToString()
        => $"OutboxItem[{Id}] {EntityType}:{EntityId} Attempts:{AttemptCount} Sent:{SentAt?.ToString("u") ?? "pending"}";
}
