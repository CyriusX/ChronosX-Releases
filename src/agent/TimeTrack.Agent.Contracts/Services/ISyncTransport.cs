using TimeTrack.Agent.Domain.Entities;

namespace TimeTrack.Agent.Contracts.Services;

/// <summary>
/// Resultado do envio de batch para o backend
/// </summary>
public sealed class SyncResult
{
    /// <summary>
    /// Se o envio foi bem sucedido (HTTP 2xx)
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// IDs dos itens que foram processados com sucesso
    /// </summary>
    public IReadOnlyList<Guid> ProcessedIds { get; init; } = Array.Empty<Guid>();

    /// <summary>
    /// Pares (outboxId, errorMessage) de itens que o backend rejeitou no nível
    /// individual (HTTP 200 com erros[]) — devem permanecer no outbox para retry
    /// com backoff em vez de serem silenciosamente marcados como enviados.
    /// </summary>
    public IReadOnlyList<(Guid OutboxId, string Error)> FailedItems { get; init; }
        = Array.Empty<(Guid, string)>();

    /// <summary>
    /// Mensagem de erro (se houver)
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Código de status HTTP
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Quantidade de itens processados
    /// </summary>
    public int ProcessedCount { get; init; }

    /// <summary>
    /// Quantidade de duplicados
    /// </summary>
    public int DuplicatesCount { get; init; }

    public static SyncResult Success(int processedCount, int duplicatesCount, IReadOnlyList<Guid> processedIds)
    {
        return new SyncResult
        {
            IsSuccess = true,
            ProcessedCount = processedCount,
            DuplicatesCount = duplicatesCount,
            ProcessedIds = processedIds,
            StatusCode = 200
        };
    }

    public static SyncResult PartialSuccess(
        int processedCount,
        int duplicatesCount,
        IReadOnlyList<Guid> processedIds,
        IReadOnlyList<(Guid OutboxId, string Error)> failedItems)
    {
        return new SyncResult
        {
            IsSuccess = true,
            ProcessedCount = processedCount,
            DuplicatesCount = duplicatesCount,
            ProcessedIds = processedIds,
            FailedItems = failedItems,
            StatusCode = 200
        };
    }

    public static SyncResult Failure(string error, int? statusCode = null)
    {
        return new SyncResult
        {
            IsSuccess = false,
            ErrorMessage = error,
            StatusCode = statusCode
        };
    }
}

/// <summary>
/// Interface para transporte de dados para o backend
/// </summary>
public interface ISyncTransport
{
    /// <summary>
    /// Envia um batch de activity sessions para o backend
    /// </summary>
    Task<SyncResult> SendActivitySessionsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um batch de idle periods para o backend
    /// </summary>
    Task<SyncResult> SendIdlePeriodsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um batch de justificativas de idle para o backend
    /// </summary>
    Task<SyncResult> SendIdleJustificationsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um batch de focus sessions para o backend
    /// </summary>
    Task<SyncResult> SendFocusSessionsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um batch de agent events para o backend
    /// </summary>
    Task<SyncResult> SendAgentEventsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Envia um batch de machine metrics para o backend
    /// </summary>
    Task<SyncResult> SendMachineMetricsAsync(
        IEnumerable<OutboxItem> items,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica conectividade com o backend
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
