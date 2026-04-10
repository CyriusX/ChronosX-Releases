using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TimeTrack.Agent.Contracts.Repositories;
using TimeTrack.Agent.Contracts.Services;
using static TimeTrack.Agent.Contracts.Services.AgentEventCategory;
using static TimeTrack.Agent.Contracts.Services.AgentEventSeverity;
using TimeTrack.AgentService.Ipc;

namespace TimeTrack.AgentService.Workers;

/// <summary>
/// Background service that monitors agent health and broadcasts status changes to connected IPC clients.
///
/// SOLID:
/// - SRP: Only monitors agent health and broadcasts events
/// - OCP: Extensible for other health metrics
/// - DIP: Depends on IIpcServer and repository abstractions
/// </summary>
public sealed class AgentStatusEventBroadcaster : BackgroundService
{
    private readonly IIpcServer _ipcServer;
    private readonly IOutboxRepository _outboxRepository;
    private readonly ISyncErrorRepository _syncErrorRepository;
    private readonly ISyncTransport _syncTransport;
    private readonly ITrackingStateRepository _trackingStateRepository;
    private readonly ICurrentUserContext _userContext;
    private readonly IAgentEventLogger _eventLogger;
    private readonly ILogger<AgentStatusEventBroadcaster> _logger;

    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);
    private string _lastHealthStatus = "unknown";
    private bool _lastBackendReachable = true;
    private int _lastPendingCount = 0;

    /// <summary>Current health status for external consumption (e.g. heartbeat payload).</summary>
    public string CurrentHealthStatus => _lastHealthStatus;

    /// <summary>Whether the backend was reachable on the last check.</summary>
    public bool CurrentBackendReachable => _lastBackendReachable;

    public AgentStatusEventBroadcaster(
        IIpcServer ipcServer,
        IOutboxRepository outboxRepository,
        ISyncErrorRepository syncErrorRepository,
        ISyncTransport syncTransport,
        ITrackingStateRepository trackingStateRepository,
        ICurrentUserContext userContext,
        IAgentEventLogger eventLogger,
        ILogger<AgentStatusEventBroadcaster> logger)
    {
        _ipcServer = ipcServer;
        _outboxRepository = outboxRepository;
        _syncErrorRepository = syncErrorRepository;
        _syncTransport = syncTransport;
        _trackingStateRepository = trackingStateRepository;
        _userContext = userContext;
        _eventLogger = eventLogger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AgentStatusEventBroadcaster started");

        try
        {
            // Initial delay to let other services start
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndBroadcastStatusAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking agent status");
                }

                await Task.Delay(_checkInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }

        _logger.LogInformation("AgentStatusEventBroadcaster stopped");
    }

    private async Task CheckAndBroadcastStatusAsync(CancellationToken cancellationToken)
    {
        // Check backend connectivity
        bool backendReachable;
        try
        {
            backendReachable = await _syncTransport.CheckHealthAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Backend health check failed");
            backendReachable = false;
        }

        // Get pending items count
        var hasPending = await _outboxRepository.HasPendingItemsAsync(cancellationToken);
        var pendingItems = hasPending ? 1 : 0; // Simplified count

        // Get failed items count (errors in last hour)
        var failedItems = await _syncErrorRepository.CountConsecutiveFailuresAsync(cancellationToken);

        // Determine health status
        var healthStatus = DetermineHealthStatus(backendReachable, pendingItems, failedItems);

        // Check if we should broadcast (status changed or periodic update)
        var shouldBroadcast = healthStatus != _lastHealthStatus ||
                              backendReachable != _lastBackendReachable ||
                              pendingItems != _lastPendingCount;

        // Log backend connectivity transitions
        if (backendReachable != _lastBackendReachable)
        {
            if (!backendReachable)
            {
                await _eventLogger.LogAsync("backend.unreachable", AgentEventCategory.System, AgentEventSeverity.Error,
                    "Backend inacessível", cancellationToken: cancellationToken);
            }
            else if (_lastHealthStatus != "unknown") // Don't log on first check
            {
                await _eventLogger.LogAsync("backend.restored", AgentEventCategory.System, AgentEventSeverity.Info,
                    "Conectividade com backend restaurada", cancellationToken: cancellationToken);
            }
        }

        // Log health status transitions
        if (healthStatus != _lastHealthStatus && _lastHealthStatus != "unknown")
        {
            var severity = healthStatus == "unhealthy" ? AgentEventSeverity.Error
                         : healthStatus == "degraded" ? AgentEventSeverity.Warning
                         : AgentEventSeverity.Info;
            await _eventLogger.LogAsync("health.changed", AgentEventCategory.System, severity,
                $"Status de saúde alterado: {_lastHealthStatus} → {healthStatus}",
                new { from = _lastHealthStatus, to = healthStatus }, cancellationToken);
        }

        if (shouldBroadcast && _ipcServer.IsClientConnected)
        {
            await BroadcastHealthChangedAsync(healthStatus, backendReachable, pendingItems, failedItems, cancellationToken);

            _lastHealthStatus = healthStatus;
            _lastBackendReachable = backendReachable;
            _lastPendingCount = pendingItems;
        }
        else
        {
            _lastHealthStatus = healthStatus;
            _lastBackendReachable = backendReachable;
            _lastPendingCount = pendingItems;
        }
    }

    private static string DetermineHealthStatus(bool backendReachable, int pendingItems, int failedItems)
    {
        // Backend unreachable = can't sync at all
        if (!backendReachable)
            return "unhealthy";

        // failedItems = errors since the last successful sync (resets to 0 on any success).
        // 1-2 failures = degraded (transient issue, still recovering)
        // 3+ failures   = unhealthy (persistent problem, needs attention)
        // 0 failures    = healthy, regardless of items waiting in the outbox queue.
        // Pending outbox items are intentionally NOT used here: they accumulate normally
        // between sync cycles and would cause constant false-positive degraded status.
        if (failedItems >= 3)
            return "unhealthy";

        if (failedItems > 0)
            return "degraded";

        return "healthy";
    }

    private async Task BroadcastHealthChangedAsync(
        string status,
        bool backendReachable,
        int pendingItems,
        int failedItems,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Broadcasting AgentHealthChanged: Status={Status}, BackendReachable={BackendReachable}, Pending={Pending}, Failed={Failed}",
                status, backendReachable, pendingItems, failedItems);

            var payload = new
            {
                status,
                backendReachable,
                pendingItems,
                failedItems,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "agentHealthChanged",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);

            _logger.LogDebug("AgentHealthChanged event broadcasted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting AgentHealthChanged event");
        }
    }

    /// <summary>
    /// Broadcasts a sync progress event (called by SyncWorker)
    /// </summary>
    public async Task BroadcastSyncProgressAsync(
        string syncStatus,
        int progress,
        string? message,
        CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
            return;

        try
        {
            _logger.LogDebug(
                "Broadcasting SyncProgressChanged: Status={Status}, Progress={Progress}%",
                syncStatus, progress);

            var payload = new
            {
                status = syncStatus,
                progress,
                message,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "syncProgressChanged",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting SyncProgressChanged event");
        }
    }

    /// <summary>
    /// Broadcasts a connection state change event
    /// </summary>
    public async Task BroadcastConnectionStateChangedAsync(
        bool isConnected,
        int reconnectAttempts,
        CancellationToken cancellationToken = default)
    {
        if (!_ipcServer.IsClientConnected)
            return;

        try
        {
            _logger.LogInformation(
                "Broadcasting ConnectionStateChanged: IsConnected={IsConnected}, ReconnectAttempts={Attempts}",
                isConnected, reconnectAttempts);

            var payload = new
            {
                isConnected,
                reconnectAttempts,
                timestamp = DateTime.UtcNow.ToString("O")
            };

            var ipcEvent = new IpcEvent
            {
                EventType = "connectionStateChanged",
                Payload = payload
            };

            await _ipcServer.SendEventAsync(ipcEvent, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting ConnectionStateChanged event");
        }
    }
}
