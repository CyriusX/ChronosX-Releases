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
    private readonly IActivitySessionRepository _sessionRepository;
    private readonly IIdlePeriodRepository _idlePeriodRepository;
    private readonly IFocusCycleRepository _focusCycleRepository;
    private readonly ISyncErrorRepository _syncErrorRepository;
    private readonly ISyncTransport _syncTransport;
    private readonly ICurrentUserContext _userContext;
    private readonly IDeviceActivationService _deviceActivationService;
    private readonly ITokenStore _tokenStore;
    private readonly AgentStatusEventBroadcaster _statusBroadcaster;

    private int _consecutiveFailures;
    private DateTime? _lastSuccessfulSync;
    private DateTime _lastCleanup = DateTime.MinValue;

    public SyncWorker(
        ILogger<SyncWorker> logger,
        AgentSettings settings,
        IOutboxRepository outboxRepository,
        IActivitySessionRepository sessionRepository,
        IIdlePeriodRepository idlePeriodRepository,
        IFocusCycleRepository focusCycleRepository,
        ISyncErrorRepository syncErrorRepository,
        ISyncTransport syncTransport,
        ICurrentUserContext userContext,
        IDeviceActivationService deviceActivationService,
        ITokenStore tokenStore,
        AgentStatusEventBroadcaster statusBroadcaster)
    {
        _logger = logger;
        _settings = settings;
        _outboxRepository = outboxRepository;
        _sessionRepository = sessionRepository;
        _idlePeriodRepository = idlePeriodRepository;
        _focusCycleRepository = focusCycleRepository;
        _syncErrorRepository = syncErrorRepository;
        _syncTransport = syncTransport;
        _userContext = userContext;
        _deviceActivationService = deviceActivationService;
        _tokenStore = tokenStore;
        _statusBroadcaster = statusBroadcaster;
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

            // Cleanup old data on startup — SQLite should only hold today's data + cache.
            // Runs independently of authentication: stale data from previous sessions must go.
            await CleanupOldDataAsync(stoppingToken);

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

        // Guid.Empty means the JWT was refreshed from a web-login token (DeviceId placeholder).
        // Treat it the same as missing — the device has not been properly activated yet.
        if (_userContext.DeviceId.HasValue && _userContext.DeviceId.Value != Guid.Empty)
            return true;

        _logger.LogWarning(
            "JWT is missing device_id claim (or has empty placeholder). Attempting device activation to unblock sync.");

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

            // Emit sync started event
            await _statusBroadcaster.BroadcastSyncProgressAsync("in_progress", 0, "Iniciando sincronização...", cancellationToken);

            // Obter itens pendentes
            var pendingItems = await _outboxRepository.GetPendingAsync(
                _settings.Sync.MaxBatchSize,
                cancellationToken);

            if (!pendingItems.Any())
            {
                return;
            }

            var totalItems = pendingItems.Count();

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

            var totalSentIds = new List<Guid>();
            var hasFailures = false;

            // Processar activity sessions
            if (activitySessions.Any())
            {
                // Emit progress
                var progress = (int)((double)totalSentIds.Count / totalItems * 100);
                await _statusBroadcaster.BroadcastSyncProgressAsync("in_progress", progress, "Sincronizando sessões de atividade...", cancellationToken);

                var result = await ProcessBatchAsync(
                    activitySessions,
                    batch => _syncTransport.SendActivitySessionsAsync(batch, cancellationToken),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    // Mark this type's items immediately — don't wait for other types
                    await _outboxRepository.MarkAsSentAsync(result.ProcessedIds, cancellationToken);
                    totalSentIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }
            }

            // Processar idle periods
            if (idlePeriods.Any())
            {
                var progress = (int)((double)totalSentIds.Count / totalItems * 100);
                await _statusBroadcaster.BroadcastSyncProgressAsync("in_progress", progress, "Sincronizando períodos de inatividade...", cancellationToken);

                var result = await ProcessBatchAsync(
                    idlePeriods,
                    batch => _syncTransport.SendIdlePeriodsAsync(batch, cancellationToken),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    await _outboxRepository.MarkAsSentAsync(result.ProcessedIds, cancellationToken);
                    totalSentIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }
            }

            // Processar focus sessions
            if (focusSessions.Any())
            {
                var progress = (int)((double)totalSentIds.Count / totalItems * 100);
                await _statusBroadcaster.BroadcastSyncProgressAsync("in_progress", progress, "Sincronizando sessões de foco...", cancellationToken);

                var result = await ProcessBatchAsync(
                    focusSessions,
                    batch => _syncTransport.SendFocusSessionsAsync(batch, cancellationToken),
                    cancellationToken);

                if (result.IsSuccess)
                {
                    await _outboxRepository.MarkAsSentAsync(result.ProcessedIds, cancellationToken);
                    totalSentIds.AddRange(result.ProcessedIds);
                }
                else
                {
                    hasFailures = true;
                }
            }

            // Atualizar estado
            if (totalSentIds.Any())
            {
                _consecutiveFailures = 0;
                _lastSuccessfulSync = DateTime.UtcNow;

                _logger.LogInformation(
                    "Sync concluído. {Count} itens sincronizados{Partial}",
                    totalSentIds.Count,
                    hasFailures ? " (parcial — alguns tipos falharam)" : string.Empty);

                var statusMsg = hasFailures
                    ? $"{totalSentIds.Count} itens sincronizados (parcial)"
                    : $"{totalSentIds.Count} itens sincronizados";
                await _statusBroadcaster.BroadcastSyncProgressAsync("completed", 100, statusMsg, cancellationToken);

                // Cleanup old synced data (run at most once per hour)
                await CleanupOldDataAsync(cancellationToken);
            }

            if (hasFailures && totalSentIds.Count == 0)
            {
                _consecutiveFailures++;

                // Emit sync failed event
                await _statusBroadcaster.BroadcastSyncProgressAsync("failed", 0, "Falha na sincronização", cancellationToken);

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

            // Emit sync failed event
            await _statusBroadcaster.BroadcastSyncProgressAsync("failed", 0, ex.Message, cancellationToken);
        }
    }

    /// <summary>
    /// Sends a batch, splitting it into smaller chunks if it exceeds the byte limit.
    /// <paramref name="sendFactory"/> receives the exact sub-batch to send, preventing
    /// closure capture bugs where the full original list was always sent.
    /// </summary>
    private async Task<SyncResult> ProcessBatchAsync(
        List<OutboxItem> items,
        Func<List<OutboxItem>, Task<SyncResult>> sendFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            var estimatedSize = items.Sum(i => i.PayloadJson.Length * 2); // UTF-16 chars

            if (estimatedSize > _settings.Sync.MaxBatchSizeBytes)
            {
                return await ProcessSplitBatchAsync(items, sendFactory, cancellationToken);
            }

            var result = await sendFactory(items);

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
        Func<List<OutboxItem>, Task<SyncResult>> sendFactory,
        CancellationToken cancellationToken)
    {
        var allProcessedIds = new List<Guid>();
        var hasFailures = false;

        var currentBatch = new List<OutboxItem>();
        var currentSize = 0;

        foreach (var item in items)
        {
            var itemSize = item.PayloadJson.Length * 2;

            if (currentSize + itemSize > _settings.Sync.MaxBatchSizeBytes && currentBatch.Any())
            {
                var result = await SendChunkAsync(currentBatch, sendFactory, cancellationToken);
                if (result.IsSuccess) allProcessedIds.AddRange(result.ProcessedIds);
                else hasFailures = true;

                currentBatch.Clear();
                currentSize = 0;
            }

            currentBatch.Add(item);
            currentSize += itemSize;
        }

        if (currentBatch.Any())
        {
            var finalResult = await SendChunkAsync(currentBatch, sendFactory, cancellationToken);
            if (finalResult.IsSuccess) allProcessedIds.AddRange(finalResult.ProcessedIds);
            else hasFailures = true;
        }

        return hasFailures
            ? SyncResult.Failure("Partial failure in split batch")
            : SyncResult.Success(allProcessedIds.Count, 0, allProcessedIds);
    }

    private async Task<SyncResult> SendChunkAsync(
        List<OutboxItem> chunk,
        Func<List<OutboxItem>, Task<SyncResult>> sendFactory,
        CancellationToken cancellationToken)
    {
        var result = await sendFactory(chunk);

        if (!result.IsSuccess)
        {
            foreach (var item in chunk)
            {
                await _outboxRepository.MarkAsFailedAsync(
                    item.Id,
                    result.ErrorMessage ?? "Unknown error",
                    cancellationToken);
            }
        }

        return result;
    }

    /// <summary>
    /// Cleans up old synced data from SQLite to keep the database lean.
    /// Past data is in the cloud — local SQLite only needs today's data.
    /// Runs at most once per hour, and also once at startup.
    /// Does not require authentication — stale data from previous sessions must be removed regardless.
    /// </summary>
    private async Task CleanupOldDataAsync(CancellationToken cancellationToken)
    {
        // Run at most once per hour
        if ((DateTime.UtcNow - _lastCleanup).TotalHours < 1)
            return;

        try
        {
            // Use LOCAL machine time so that "today" aligns with the user's timezone.
            // Previously used DateTime.UtcNow.Date which caused sessions from today (in the
            // user's timezone) to be deleted when UTC date rolls over before local date.
            // E.g., in UTC-3 (São Paulo), UTC midnight is 21:00 local — sessions from
            // 21:00-23:59 local would be purged as "yesterday" in UTC.
            var todayStart = DateTime.Today.ToUniversalTime();

            // 1. Remove sent outbox items older than 24h
            var outboxRemoved = await _outboxRepository.RemoveSentOlderThanAsync(
                TimeSpan.FromHours(24), cancellationToken);

            // 2. Remove activity sessions from before today (already synced to cloud)
            var sessionsRemoved = await _sessionRepository.DeleteOlderThanAsync(todayStart, cancellationToken);

            // 3. Remove idle periods from before today
            var idleRemoved = await _idlePeriodRepository.DeleteOlderThanAsync(todayStart, cancellationToken);

            // 4. Remove completed+synced focus cycles from before today
            var focusRemoved = await _focusCycleRepository.DeleteOlderThanAsync(todayStart, cancellationToken);

            // 5. Remove sync errors older than 30 days
            await _syncErrorRepository.CleanupOldErrorsAsync(30, cancellationToken);

            _lastCleanup = DateTime.UtcNow;

            if (outboxRemoved + sessionsRemoved + idleRemoved + focusRemoved > 0)
            {
                _logger.LogInformation(
                    "SQLite cleanup: {Outbox} outbox, {Sessions} sessions, {Idle} idle periods, {Focus} focus cycles removed",
                    outboxRemoved, sessionsRemoved, idleRemoved, focusRemoved);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQLite cleanup failed (non-critical)");
        }
    }
}
