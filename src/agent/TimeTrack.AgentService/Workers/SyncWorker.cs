using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Configuration;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using TimeTrack.Agent.Domain.Entities;
using TimeTrack.AgentService.Configuration;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Worker de sincronização que consome o Outbox e envia batches para o backend
/// </summary>
public sealed class SyncWorker : BackgroundService
{
    private readonly ILogger<SyncWorker> _logger;
    private readonly AgentSettings _settings;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISyncTransport _syncTransport;
    private readonly ICurrentUserContext _userContext;
    private readonly IDeviceActivationService _deviceActivationService;
    private readonly ITokenStore _tokenStore;

    private int _consecutiveFailures;
    private DateTime? _lastSuccessfulSync;

    public SyncWorker(
        ILogger<SyncWorker> logger,
        AgentSettings settings,
        IOutboxRepository outboxRepository,
        ISyncTransport syncTransport,
        ICurrentUserContext userContext,
        IDeviceActivationService deviceActivationService,
        ITokenStore tokenStore)
    {
        _logger = logger;
        _settings = settings;
        _outboxRepository = outboxRepository;
        _syncTransport = syncTransport;
        _userContext = userContext;
        _deviceActivationService = deviceActivationService;
        _tokenStore = tokenStore;
    }

    /// <summary>
    /// Número de falhas consecutivas (para health checks)
    /// </summary>
    public int ConsecutiveFailures => _consecutiveFailures;

    /// <summary>
    /// Última sincronização bem sucedida
    /// </summary>
    public DateTime? LastSuccessfulSync => _lastSuccessfulSync;

    /// <summary>
    /// Indica se o worker está em estado crítico (muitas falhas)
    /// </summary>
    public bool IsCritical => _consecutiveFailures >= _settings.Sync.ConsecutiveFailuresAlertThreshold;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "SyncWorker iniciando. Intervalo: {Interval}s, MaxBatch: {MaxBatch}",
            _settings.Sync.SyncIntervalSeconds,
            _settings.Sync.MaxBatchSize);

        using var periodicTimer = new PeriodicTimer(
            TimeSpan.FromSeconds(_settings.Sync.SyncIntervalSeconds));

        try
        {
            // Redefinir itens presos no outbox ao iniciar (recuperação de backoff acumulado)
            var resetCount = await _outboxRepository.ResetStuckItemsAsync(stoppingToken);
            if (resetCount > 0)
            {
                _logger.LogInformation(
                    "SyncWorker: {Count} itens do outbox presos foram redefinidos na inicialização.",
                    resetCount);
            }

            // Primeira execução imediata
            await ExecuteSyncCycleAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested &&
                   await periodicTimer.WaitForNextTickAsync(stoppingToken))
            {
                await ExecuteSyncCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "SyncWorker crashed");
            throw;
        }

        _logger.LogInformation("SyncWorker encerrado");
    }

    /// <summary>
    /// Ensures the current token includes the device_id claim required by the backend ingest endpoints.
    /// If the claim is missing, attempts device activation to obtain a new token with device_id.
    /// </summary>
    private async Task<bool> EnsureDeviceActivatedAsync(CancellationToken cancellationToken)
    {
        if (!_userContext.IsAuthenticated)
            return false;

        if (_userContext.DeviceId.HasValue)
            return true;

        _logger.LogWarning(
            "JWT is missing device_id claim. Attempting device activation to unblock sync.");

        var jwt = await _tokenStore.GetJwtAsync(cancellationToken);
        var refreshToken = await _tokenStore.GetRefreshTokenAsync(cancellationToken);

        if (string.IsNullOrEmpty(jwt))
        {
            _logger.LogError("Cannot activate device: no JWT available.");
            return false;
        }

        var result = await _deviceActivationService.ActivateDeviceAsync(
            jwt,
            refreshToken ?? string.Empty,
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogError(
                "Device activation failed: {Error}. Sync will remain blocked until token contains device_id.",
                result.ErrorMessage);
            return false;
        }

        await _userContext.RefreshAsync(cancellationToken);

        _logger.LogInformation(
            "Device activated successfully (DeviceId={DeviceId}). Sync can now proceed.",
            result.DeviceId);

        return _userContext.DeviceId.HasValue;
    }

    private async Task ExecuteSyncCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Garantir que o token possui device_id antes de tentar sincronizar
            if (!await EnsureDeviceActivatedAsync(cancellationToken))
            {
                _logger.LogDebug("Skipping sync cycle: user not authenticated or device not activated.");
                return;
            }

            // Verificar se há itens pendentes
            if (!await _outboxRepository.HasPendingItemsAsync(cancellationToken))
            {
                _logger.LogDebug("Nenhum item pendente no outbox");
                return;
            }

            // Obter itens pendentes
            var pendingItems = await _outboxRepository.GetPendingAsync(
                _settings.Sync.MaxBatchSize,
                cancellationToken);

            if (!pendingItems.Any())
            {
                return;
            }

            // Separar por tipo de entidade
            var activitySessions = pendingItems
                .Where(i => i.EntityType == "activity_session")
                .ToList();

            var idlePeriods = pendingItems
                .Where(i => i.EntityType == "idle_period")
                .ToList();

            var focusSessions = pendingItems
                .Where(i => i.EntityType == "focus_session")
                .ToList();

            _logger.LogInformation(
                "Processando batch: {ActivityCount} activity sessions, {IdleCount} idle periods, {FocusCount} focus sessions",
                activitySessions.Count,
                idlePeriods.Count,
                focusSessions.Count);

            var allProcessedIds = new List<Guid>();
            var hasFailures = false;

            // Processar activity sessions
            if (activitySessions.Any())
            {
                var result = await ProcessBatchAsync(
                    activitySessions,
                    () => _syncTransport.SendActivitySessionsAsync(activitySessions, cancellationToken),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    allProcessedIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }
            }

            // Processar idle periods
            if (idlePeriods.Any())
            {
                var result = await ProcessBatchAsync(
                    idlePeriods,
                    () => _syncTransport.SendIdlePeriodsAsync(idlePeriods, cancellationToken),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    allProcessedIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }
            }

            // Processar focus sessions
            if (focusSessions.Any())
            {
                var result = await ProcessBatchAsync(
                    focusSessions,
                    () => _syncTransport.SendFocusSessionsAsync(focusSessions, cancellationToken),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    allProcessedIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }
            }

            // Atualizar estado
            if (!hasFailures && allProcessedIds.Any())
            {
                await _outboxRepository.MarkAsSentAsync(allProcessedIds, cancellationToken);
                _consecutiveFailures = 0;
                _lastSuccessfulSync = DateTime.UtcNow;

                _logger.LogInformation(
                    "Sync concluído com sucesso. {Count} itens sincronizados",
                    allProcessedIds.Count);
            }
            else if (hasFailures)
            {
                _consecutiveFailures++;

                if (IsCritical)
                {
                    _logger.LogCritical(
                        "ALERTA: {Count} falhas consecutivas de sync. Verificar conectividade.",
                        _consecutiveFailures);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro no ciclo de sync");
            _consecutiveFailures++;
        }
    }

    private async Task<SyncResult> ProcessBatchAsync(
        List<OutboxItem> items,
        Func<Task<SyncResult>> sendFunc,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verificar tamanho do batch em bytes
            var estimatedSize = items.Sum(i => i.PayloadJson.Length * 2); // UTF-16 chars

            if (estimatedSize > _settings.Sync.MaxBatchSizeBytes)
            {
                // Dividir o batch se necessário
                return await ProcessSplitBatchAsync(items, sendFunc, cancellationToken);
            }

            var result = await sendFunc();

            if (!result.IsSuccess)
            {
                // Marcar todos os itens como falhados
                foreach (var item in items)
                {
                    await _outboxRepository.MarkAsFailedAsync(
                        item.Id,
                        result.ErrorMessage ?? "Unknown error",
                        cancellationToken);
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar batch");

            foreach (var item in items)
            {
                await _outboxRepository.MarkAsFailedAsync(
                    item.Id,
                    ex.Message,
                    cancellationToken);
            }

            return SyncResult.Failure(ex.Message);
        }
    }

    private async Task<SyncResult> ProcessSplitBatchAsync(
        List<OutboxItem> items,
        Func<Task<SyncResult>> sendFunc,
        CancellationToken cancellationToken)
    {
        var allProcessedIds = new List<Guid>();
        var hasFailures = false;

        // Dividir em chunks menores
        var currentBatch = new List<OutboxItem>();
        var currentSize = 0;

        foreach (var item in items)
        {
            var itemSize = item.PayloadJson.Length * 2;

            if (currentSize + itemSize > _settings.Sync.MaxBatchSizeBytes &&
                currentBatch.Any())
            {
                // Processar batch atual
                var result = await SendBatchAsync(currentBatch, sendFunc, cancellationToken);

                if (result.IsSuccess)
                {
                    allProcessedIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }

                currentBatch.Clear();
                currentSize = 0;
            }

            currentBatch.Add(item);
            currentSize += itemSize;
        }

        // Processar batch final
        if (currentBatch.Any())
        {
            var finalResult = await SendBatchAsync(currentBatch, sendFunc, cancellationToken);

            if (finalResult.IsSuccess)
            {
                allProcessedIds.AddRange(finalResult.ProcessedIds);
            }
            else
            {
                hasFailures = true;
            }
        }

        return hasFailures
            ? SyncResult.Failure("Partial failure in split batch")
            : SyncResult.Success(allProcessedIds.Count, 0, allProcessedIds);
    }

    private async Task<SyncResult> SendBatchAsync(
        List<OutboxItem> items,
        Func<Task<SyncResult>> sendFunc,
        CancellationToken cancellationToken)
    {
        var result = await sendFunc();

        if (!result.IsSuccess)
        {
            foreach (var item in items)
            {
                await _outboxRepository.MarkAsFailedAsync(
                    item.Id,
                    result.ErrorMessage ?? "Unknown error",
                    cancellationToken);
            }
        }

        return result;
    }
}
