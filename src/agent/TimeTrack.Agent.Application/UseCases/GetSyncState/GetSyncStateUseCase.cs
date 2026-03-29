using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;

namespace TimeTrack.Agent.Application.UseCases.GetSyncState;

/// <summary>
/// Response com o estado atual de sincronização
/// Campos nomeados para compatibilidade com o frontend IPC
/// </summary>
public sealed class SyncStateResponse
{
    /// <summary>
    /// Status geral: synced, pending, syncing, failed, offline
    /// </summary>
    public string Status { get; init; } = "pending";

    /// <summary>
    /// Timestamp do último sync bem-sucedido (formato ISO 8601)
    /// </summary>
    public string? LastSyncAt { get; init; }

    /// <summary>
    /// Quantidade de itens pendentes no Outbox
    /// </summary>
    public int PendingItems { get; init; }

    /// <summary>
    /// Quantidade de itens que falharam
    /// </summary>
    public int FailedItems { get; init; }

    /// <summary>
    /// Próximo sync previsto (opcional)
    /// </summary>
    public string? NextSyncAt { get; init; }

    /// <summary>
    /// Se o backend está acessível
    /// </summary>
    public bool BackendReachable { get; init; }

    /// <summary>
    /// Último erro registrado (se houver)
    /// </summary>
    public SyncErrorInfo? LastError { get; init; }
}

/// <summary>
/// Informações sobre um erro de sync
/// </summary>
public sealed class SyncErrorInfo
{
    public DateTime TimestampUtc { get; init; }
    public string Endpoint { get; init; } = string.Empty;
    public int StatusCode { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
}

/// <summary>
/// Use case para obter o estado atual de sincronização
/// </summary>
public sealed class GetSyncStateUseCase
{
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISyncErrorRepository _syncErrorRepository;
    private readonly ISyncTransport _syncTransport;

    public GetSyncStateUseCase(
        IOutboxRepository outboxRepository,
        ISyncErrorRepository syncErrorRepository,
        ISyncTransport syncTransport)
    {
        _outboxRepository = outboxRepository;
        _syncErrorRepository = syncErrorRepository;
        _syncTransport = syncTransport;
    }

    public async Task<SyncStateResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        // Get pending items count
        var pendingItems = await _outboxRepository.GetPendingAsync(1000, cancellationToken);
        var pendingCount = pendingItems.Count();

        // Get failed items count (errors in the last hour)
        var failedCount = await _syncErrorRepository.CountConsecutiveFailuresAsync(cancellationToken);

        // Get last error
        var lastError = await _syncErrorRepository.GetLatestAsync(cancellationToken);

        // Check backend health
        var backendReachable = await _syncTransport.CheckHealthAsync(cancellationToken);

        // Get last successful sync from outbox (items that were sent)
        var lastSuccessfulSyncAt = await GetLastSuccessfulSyncTimeAsync(cancellationToken);

        // Determine overall status
        var status = DetermineStatus(pendingCount, failedCount, lastError, backendReachable);

        return new SyncStateResponse
        {
            Status = status,
            LastSyncAt = lastSuccessfulSyncAt?.ToString("O"),
            PendingItems = pendingCount,
            FailedItems = failedCount,
            NextSyncAt = null, // Could be calculated based on sync interval
            BackendReachable = backendReachable,
            LastError = lastError is not null ? new SyncErrorInfo
            {
                TimestampUtc = lastError.TimestampUtc,
                Endpoint = lastError.Endpoint,
                StatusCode = lastError.StatusCode,
                ErrorMessage = lastError.ErrorMessage
            } : null
        };
    }

    private async Task<DateTime?> GetLastSuccessfulSyncTimeAsync(CancellationToken cancellationToken)
    {
        // Get the most recent sent item from outbox
        var sentItems = await _outboxRepository.GetSentAsync(1, cancellationToken);
        return sentItems.FirstOrDefault()?.SentAt;
    }

    private static string DetermineStatus(
        int pendingCount,
        int failedCount,
        Domain.Entities.SyncError? lastError,
        bool backendReachable)
    {
        if (!backendReachable)
            return "failed";

        if (failedCount > 0 || (lastError is not null && lastError.TimestampUtc > DateTime.UtcNow.AddHours(-1)))
            return "failed";

        if (pendingCount > 0)
            return "pending";

        return "synced";
    }
}
