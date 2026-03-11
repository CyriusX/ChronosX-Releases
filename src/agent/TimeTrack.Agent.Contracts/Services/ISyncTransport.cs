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
    /// Verifica conectividade com o backend
    /// </summary>
    Task<bool> CheckHealthAsync(CancellationToken cancellationToken = default);
}
